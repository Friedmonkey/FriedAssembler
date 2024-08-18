using FriedLexer;
using System;
using System.Linq;
using System.Text;
using static FriedAssembler.PreAssembler;


namespace FriedAssembler;
public class PreAssembler : AnalizerBase<char>
{
    private static readonly string IncludeDataSegmentMarker = ";%includeData$egmentMarker%";
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
    public string Parse(string input) 
    {
        input = input + "\n #segment .reloc";
        this.Analizable = input.ToList();
        this.Position = 0;

        //we always parse segments
        input = SegmentParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;

        input = IncludePreParser(input);
        this.Analizable = input.ToList();
        this.Position = 0;

        input = IncludeGenerator(input);
        this.Analizable = input.ToList();
        this.Position = 0;


        return input;
    }

    //generates regions
    private string SegmentParser(string input) 
    {
        string output = string.Empty;

        while (Safe)
        {
            if (Find("#segment "))
            {
                string segmentName = ConsumeUntilWhitespace().ToLower();
                output += segmentName switch
                {
                    ".text" => "section '.text' code readable executable",
                    ".data" => "section '.data' data readable writeable",
                    ".idata" => "section '.idata' import data readable writeable \n " + IncludeDataSegmentMarker,
                    ".reloc" => "section '.reloc' fixups data readable discardable\t; needed for Win32s",
                    _ => throw new Exception($"Segment `{segmentName}` Does not exist, did you mean `.text` or `.idata`?")
                };
            }
            output += Current;
            Position++;
        }

        return output;
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
            if (Find("#include "))
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
        sb.AppendLine(";the import table");
        foreach (var include in Includes)
        {
            string dll = include.dll.Split('.').First().ToLower();
            sb.AppendLine($"\tdd 0,0,0,RVA flasm_include_{dll}_name,RVA flasm_include_{dll}_table");
        }
        sb.AppendLine("\tdd 0,0,0,0,0");


        sb.AppendLine(";table declarations");
        foreach (var include in Includes)
        {
            string dll = include.dll.Split('.').First().ToLower();
            sb.AppendLine($"flasm_include_{dll}_table:");
            foreach (var method in include.methods)
            {
                sb.AppendLine($"\t{method} dd RVA flasm_include_{method}");
            }
            sb.AppendLine($"\tdd 0                                      ; End of table");
        }

        sb.AppendLine(";include declarations");
        foreach (var include in Includes) 
        {
            string dll = include.dll.Split('.').First().ToLower();
            sb.AppendLine($"flasm_include_{dll}_name db '{include.dll}',0");
        }


        sb.AppendLine(";method declarations");
        foreach (var include in Includes)
        {
            foreach (var method in include.methods)
            {
                sb.AppendLine($"flasm_include_{method} dw 0");
                sb.AppendLine($"\tdb '{method}',0");
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
    public void SkipWhitespace()
    {
        while (Safe && char.IsWhiteSpace(Current))
        {
            Position++;
        }
    }
    public void Consume(char character)
    {
        if (Current == character)
            Position++;
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
}
