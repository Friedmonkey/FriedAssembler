using Reloaded.Assembler;
using Reloaded.Assembler.Definitions;
using System.Diagnostics;
using FriedAssembler;

namespace Frasm;

internal class Program
{
    static void Main(string[] args)
    {
        string code = """
        #include <user32.dll>
        {
            [MessageBoxA]
        }
        #include <msvcrt.dll>
        {
            [printf]
        }
        #include <kernel32.dll>
        {
            [ExitProcess]
        }

        format PE console
        entry main

        #segment .text
        main:
            ; Set up parameters for printf
            push message ; Push address of the message string (parameter)
            push format_string ; Push address of the format string
            call [printf]       ; Call printf

            ; Exit the program
            push 0              ; Exit code 0
            call [ExitProcess]  ; Call ExitProcess

        #segment .data
        format_string db '%s',0   ; Format string for printf (prints a string)
        message db 'hello world',0 ; Message to print

        #segment .idata
        
        """;

        File.WriteAllText("input.flasm", code);


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
