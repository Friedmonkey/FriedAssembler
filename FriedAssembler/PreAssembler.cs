using System;
using System.Linq;
using System.Reflection;
using System.Text;
using static FriedAssembler.PreAssembler;


namespace FriedAssembler;
public class PreAssembler : AnalizerBase<char>
{
    private static readonly string IncludeDataSegmentMarker = ";%includeData$egmentMarker%";
    private static readonly string MutableDataSegmentMarker = ";%mutableData$egmentMarker%";
    private static readonly string BufferDataSegmentMarker = ";%bufferData$egmentMarker%";
    private static readonly string ConstDataSegmentMarker = ";%constData$egmentMarker%";
    private static readonly string TextCodeSegmentMarker = ";%textCode$egmentMarker%";
    public PreAssembler() : base('\0') { }
    public class Include 
    {
        public Include() { }
        public Include(string dll, params string[] methods)
        {
            this.dll = dll;
            this.methods = methods.ToList();
        }
        public string dll;
        public List<string> methods;
    }
    List<Include> Includes = new List<Include>();
    string EntryCode = string.Empty;
    string EntryName = string.Empty;
    string Format = string.Empty;

    public class Varible
    {
        public Varible() { }
        public Varible(string name, string type, string value, bool constant, bool initialized)
        {
            this.name = name;
            this.type = type;
            this.value = value;
            this.constant = constant;
            this.initialized = initialized;
        }
        public string name;
        public string type;
        public string value;
        public bool constant = false;
        public bool initialized = false;
    }
    List<Varible> Varibles = new List<Varible>();

    public string Parse(string input) 
    {
        input = "\n" + input + "\n";
        this.Analizable = input.ToList();
        this.Position = 0;

        //we always parse segments
        input = SegmentParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;



        input = EntryParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;

        input = EntryGenerator(input);
        this.Analizable = input.ToList();
        this.Position = 0;


        input = FormatParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;

        input = FormatGenerator(input);
        this.Analizable = input.ToList();
        this.Position = 0;



        input = IncludePreParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;

        input = IncludeGenerator(input);
        this.Analizable = input.ToList();
        this.Position = 0;



        input = VariblePreParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;

        input = VaribleGenerator(input);
        this.Analizable = input.ToList();
        this.Position = 0;


        return input;
    }

    //generates regions
    private string SegmentParser(string input) 
    {
        string output = string.Empty;
        List<string> FoundFormats = new List<string>();
        void AddSegment(string segmentName)
        {
            output += segmentName switch
            {
                ".text" => "section '.text' code readable executable \n" + TextCodeSegmentMarker,
                ".data" => "section '.data' data readable writeable \n" + MutableDataSegmentMarker,
                ".bss" => "section '.bss' readable writeable \n" + BufferDataSegmentMarker,
                ".cdata" => "section '.cdata' data readable \n" + ConstDataSegmentMarker,
                ".idata" => "section '.idata' import data readable writeable \n" + IncludeDataSegmentMarker,
                ".reloc" => "section '.reloc' fixups data readable discardable\t; needed for Win32s",
                _ => throw new Exception($"Segment `{segmentName}` Does not exist, did you mean `.text` or `.idata`?")
            };
            FoundFormats.Add(segmentName);
        }


        while (Safe)
        {
            if (FindStart("#segment "))
            {
                string segmentName = ConsumeUntilWhitespace().ToLower();
                AddSegment(segmentName);
            }
            output += Current;
            Position++;
        }
        output += "\n";

        void AddSegmentIfMissing(string seg) 
        {
            if (!FoundFormats.Contains(seg))
            {
                output += "\n";
                AddSegment(seg);
                output += "\n";
            }
        }

        AddSegmentIfMissing(".text");
        //AddSegmentIfMissing(".data");
        //AddSegmentIfMissing(".bss");
        //AddSegmentIfMissing(".cdata");
        AddSegmentIfMissing(".idata");
        AddSegmentIfMissing(".reloc");

        return output;
    }
    private string EntryParser(string input)
    {
        string output = string.Empty;

        while (Safe)
        {
            if (FindStart("entry "))
            {
                string entryName = TryConsumeUntil('(');
                if (entryName is not null)
                {
                    Consume('(');
                    Consume(')');
                    SkipWhitespace();
                    Consume('{');
                    string code = ConsumeUntil('}');
                    while (!Peek(-1).IsEnter())
                    {
                        //if the prevous character before the } was not an enter meaning it wasnt at the start of the line
                        //so it was part of the code (somehow) 
                        code += Consume('}');
                        code += ConsumeUntil('}');
                    }
                    Consume('}');

                    //we have captured the code
                    EntryName = entryName;
                    EntryCode = $"{entryName}:\n{code}";
                }
            }
            output += Current;
            Position++;
        }

        return output;
    }

    private string EntryGenerator(string input)
    {
        if (!string.IsNullOrEmpty(EntryName))
        {
            input = $"entry {EntryName}\n{input}";
        }

        if (input.Contains(TextCodeSegmentMarker))
        {
            return input.Replace(TextCodeSegmentMarker, EntryCode);
        }
        throw new Exception("Trying to write the entry method but the text code segment is missing, did you forgot to add `#segment .text` ?");
        //return input.Replace(IncludeDataSegmentMarker, sb.ToString());
    }

    private string FormatParser(string input)
    {
        string output = string.Empty;

        while (Safe)
        {
            if (FindStart("#format "))
            {
                Format = ConsumeUntilEnter();
            }
            output += Current;
            Position++;
        }

        return output;
    }
    private string FormatGenerator(string input)
    {
        if (!string.IsNullOrEmpty(Format))
        {
            return $"format {Format}\n{input}";
        }

        return input;
    }

    private string VariblePreParser(string input)
    {
        //if (!input.Contains(MutableDataSegmentMarker))
        //{
        //    throw new Exception("Trying to use varibles but the varibles data segment is missing, did you forgot to add `#segment .data` ?");
        //}

        string output = string.Empty;
        while (Safe)
        {
            bool found = false;
            bool constant = false;
            bool initialized = false;
            if (FindStart("const "))
            { 
                found = true;
                constant = true;
                initialized = true;
            }

            if (FindStart("mutable "))
            {
                found = true;
                constant = false;
                initialized = true;
            }

            if (FindStart("buffer "))
            {
                found = true;
                constant = false;
                initialized = false;
            }

            if (found)
            {
                Varible varible = new Varible();
                varible.constant = constant;
                varible.initialized = initialized;
                varible.type = ConsumeUntilWhitespace();
                ConsumeWhitespace();
                varible.name = ConsumeUntilWhitespace();
                ConsumeWhitespace();
                Consume('=');
                ConsumeWhitespace();
                if (Current == '"') //we add real strings with automatically zero terminate
                {
                    Consume('"');
                    varible.value = "'";
                    //varible.value += TryConsumeUntil('\\');
                    //switch (Peek(1))
                    //{
                    //    case 'n':
                    //        varible.value += "',10,'";
                    //        break;
                    //    case '\\':
                    //        varible.value += "',92,'";
                    //        break;
                    //    default:
                    //        break;
                    //}
                    varible.value += TryConsumeUntil('"');
                    if (Current == '"')
                    {
                        Consume('"');
                        varible.value += "',0";
                        varible.value = varible.value
                            .Replace("\\n","',10,'")
                            .Replace("\\\\","',92,'")
                            .Replace(",'',", ",");
                    }
                    else
                    {
                        throw new Exception("Unterminated string, string is missing an ending quote");
                    }

                    //if (Current == ';') Consume(';');
                }
                //else if (Find("void"))
                //{
                //    if (constant)
                //    {
                //        throw new Exception("constant cannot be uninitalized");
                //    }
                //    varible.value = null; // mark as uninitialized
                //}
                else
                {
                    varible.value = ConsumeUntilEnter();
                }


                Varible? sameVarible = Varibles.FirstOrDefault(i => i.name == varible.name);
                if (sameVarible is null)
                {   //varible doest exit yet, lets add it
                    Varibles.Add(varible);
                }
                else
                {   //varible exists, lets let the user know
                    throw new Exception($"the varible `{varible.name}` already exists");
                }
            }
            output += Current;
            Position++; //we dont care about this, we keep it as it used to be
        }
        return output;
    }

    private string VaribleGenerator(string input)
    {
        StringBuilder constantSB = new StringBuilder();
        StringBuilder mutableSB = new StringBuilder();
        StringBuilder bufferSB = new StringBuilder();
        foreach (var varible in Varibles)
        {
            //string type = "d" + (varible.type.ToLower()).First();
            ////from "byte" to "db" and from "word" to "dw"
            string prefix = varible.initialized ? "d" : "r"; //declare for initalized or reserve for buffers
            string type = varible.type.ToLower() switch
            {
                "byte" => prefix+"b",
                "word" => prefix+"w",
                "dword" => prefix+"d",
                "qword" => prefix+"q",
                _ => varible.type,
            };
            //from "byte" to "db" and from "word" to "dw"
            string line = $"\t{varible.name} {type} {varible.value}";
            if (varible.constant)
            {
                constantSB.AppendLine(line);
            }
            else if (!varible.initialized)
            { 
                bufferSB.AppendLine(line);
            }
            else
            {
                mutableSB.AppendLine(line);
            }
        }

        string mutables = mutableSB.ToString();
        string constants = constantSB.ToString();
        string buffers = bufferSB.ToString();

        if (!string.IsNullOrEmpty(mutables) && !input.Contains(MutableDataSegmentMarker))
        {
            throw new Exception(".data segment missing");
        }
        if (!string.IsNullOrEmpty(constants) && !input.Contains(ConstDataSegmentMarker))
        {
            throw new Exception(".cdata segment missing");
        }
        if (!string.IsNullOrEmpty(buffers) && !input.Contains(BufferDataSegmentMarker))
        {
            throw new Exception(".bss segment missing");
        }

        return input
            .Replace(MutableDataSegmentMarker, mutables)
            .Replace(ConstDataSegmentMarker, constants)
            .Replace(BufferDataSegmentMarker, buffers);

        //throw new Exception("Trying to add varibles but the varible data segment is missing, did you forgot to add `#segment .data` or `#segment .cdata` ?");
        //return input.Replace(IncludeDataSegmentMarker, sb.ToString());
    }
    private string IncludePreParser(string input)
    {
        if (!input.Contains(IncludeDataSegmentMarker))
        {
            throw new Exception("Trying to include dlls but the imported data segment is missing, did you forgot to add `#segment .idata` ?");
        }

        string output = string.Empty;
        while (Safe)
        {
            if (FindStart("#include "))
            {
                Include include = new Include();
                include.methods = new List<string>();

                Consume('<');
                include.dll = ConsumeUntil('>');
                Consume('>');
                SkipWhitespace();
                //we got the name now get all the methods
                Consume('{');
                do
                {
                    if (Current == ',') Consume(',');
                    SkipWhitespace();
                    Consume('[');
                    string methodName = ConsumeUntil(']');
                    include.methods.Add(methodName);
                    Consume(']');
                }
                while (Current == ',');

                SkipWhitespace();
                Consume('}');

                Include? sameDll = Includes.FirstOrDefault(i => i.dll.ToLower() == include.dll.ToLower());
                if (sameDll is null)
                {   //include doest exit yet, lets add it
                    Includes.Add(include);
                }
                else
                {   //include exists, lets add these methods
                    sameDll.methods = sameDll.methods.Concat(include.methods).Distinct().ToList();
                }
            }
            output += Current;
            Position++; //we dont care about this, we keep it as it used to be
        }
        return output;
    }

    private string IncludeGenerator(string input)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("\t;the import table");
        foreach (var include in Includes)
        {
            string dll = include.dll.Split('.').First().ToLower();
            sb.AppendLine($"\tdd 0,0,0,RVA flasm_include_{dll}_name,RVA flasm_include_{dll}_table");
        }
        sb.AppendLine("\tdd 0,0,0,0,0");
        sb.AppendLine("\n");



        sb.AppendLine("\t;table declarations");
        foreach (var include in Includes)
        {
            string dll = include.dll.Split('.').First().ToLower();
            sb.AppendLine($"flasm_include_{dll}_table:");
            foreach (var method in include.methods)
            {
                sb.AppendLine($"\t{method} dd RVA flasm_include_{method}");
            }
            sb.AppendLine($"\tdd 0  ; End of table");
        }
        sb.AppendLine("\n");


        sb.AppendLine("\t;include declarations");
        foreach (var include in Includes) 
        {
            string dll = include.dll.Split('.').First().ToLower();
            sb.AppendLine($"\tflasm_include_{dll}_name db '{include.dll}',0");
        }
        sb.AppendLine("\n");


        sb.AppendLine("\t;method declarations");
        foreach (var include in Includes)
        {
            foreach (var method in include.methods)
            {
                sb.AppendLine($"\tflasm_include_{method} dw 0");
                sb.AppendLine($"\t\tdb '{method}',0");
            }
        }
        if (input.Contains(IncludeDataSegmentMarker))
        {
            string compiled = sb.ToString();
            return input.Replace(IncludeDataSegmentMarker, compiled);
        }
        throw new Exception("Trying to include dlls but the imported data segment is missing, did you forgot to add `#segment .idata` ?");
        //return input.Replace(IncludeDataSegmentMarker, sb.ToString());
    }
    public string ConsumeUntilWhitespace()
    {
        string consumed = string.Empty;
        while (Safe && !char.IsWhiteSpace(Current))
        {
            consumed += Current;
            Position++;
        }
        return consumed;
    }
    public string ConsumeUntilEnter()
    {
        string consumed = string.Empty;
        while (Safe && !Current.IsEnter())
        {
            consumed += Current;
            Position++;
        }
        return consumed;
    }
    public string ConsumeUntil(char stop)
    {
        string consumed = string.Empty;
        while (Safe && Current != stop)
        {
            consumed += Current;
            Position++;
        }
        return consumed;
    }
    public string TryConsumeUntil(char stop)
    {
        string consumed = string.Empty;
        while (Safe && Current != stop)
        {
            if (Current.IsEnter()) return null;

            consumed += Current;
            Position++;
        }
        return consumed;
    }
    public void SkipWhitespace()
    {
        while (Safe && char.IsWhiteSpace(Current))
        {
            Position++;
        }
    }
    public int ConsumeWhitespace()
    {
        int skipped = 0;
        while (Safe && char.IsWhiteSpace(Current))
        {
            Position++;
            skipped++;
        }
        return skipped;
    }
    public char Consume(char character)
    {
        if (Current == character)
        { 
            Position++;
            return character;
        }
        else
            throw new Exception($"Expected `{character}` got `{Current}` instead.");
    }
    public bool Find(string find)
    {
        for (int i = 0; i < find.Length; i++)
        {
            if (Peek(i) == find[i])
            {
                continue;
            }
            else return false;
        }
        Position += find.Length;
        return true;
    }

    public bool FindStart(string find)
    {
        if (Find(find))
        {
            int length = find.Length+1;
            if (Peek(-length).IsEnter())
            {
                return true;
            }
            else
            {
                Console.WriteLine($"`{find}` found, but was not at the start of a line");
                Position -= find.Length;
                return false;
            }
        }
        return false;
    }
}
public static class CharExtention
{
    public static bool IsEnter(this char character)
    {
        return character is '\n' or '\r';
    }
}
