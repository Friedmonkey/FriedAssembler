using Reloaded.Assembler;
using Reloaded.Assembler.Definitions;
using System.Diagnostics;
using FriedAssembler;

namespace Flasm;

internal class Program
{
    static void Main(string[] args)
    {
        //string code = """
        //#format PE console

        //#include <msvcrt.dll>
        //{
        //    [printf]
        //}
        //#include <kernel32.dll>
        //{
        //    [ExitProcess]
        //}

        //const byte print_message = "Hello world!";
        //const byte print_format = "%s";

        //entry main()
        //{
        //    push print_message
        //    push print_format
        //    call [printf]

        //    push 0
        //    call [ExitProcess]
        //}
        
        //""";

        //File.WriteAllText("input.flasm", code);


        try
        {
            string input = File.ReadAllText("input.flasm");

            var pre = new PreAssembler();
            string preOutput = pre.Parse(input);

            File.WriteAllText("generated.asm", preOutput);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message.ToString());
        }

        var assembler = new Assembler();
        try
        {
            var output = assembler.AssembleFile("generated.asm");
            File.WriteAllBytes("output.exe", output);
        }
        catch (FasmException ex)
        {
            Console.WriteLine("Error definition: {0}; Error code: {1}; Error line: {2}; Mnemonics: {3}",
                ex.ErrorCode, (int)ex.ErrorCode, ex.Line, ex.Mnemonics);
        }
        finally 
        {
            assembler.Dispose();
        }
    }
}
