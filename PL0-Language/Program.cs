// result1 = fact(5) = 120
// result2 = fib(5) = 5 
using System;
using System.IO;
using Irony.Parsing;
using PL0_Language.Gramatica;
using PL0_Language.Codegen;

namespace PL0_Language
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Programa de prueba (factorial y fibonacci)
            string code = @"
                const n = 5;
                var result1, result2;

                function fact(x: integer): integer;
                begin
                    if x = 0 then
                        return 1
                    else
                        return x * fact(x - 1)
                end;

                function fib(x: integer): integer;
                begin
                    if x = 0 then
                        return 0
                    else
                        if x = 1 then
                            return 1
                        else
                            return fib(x - 1) + fib(x - 2)
                end;

                begin
                    result1 := fact(n);    // 120
                    !result1;
                    result2 := fib(5);     // 5
                    !result2
                end.
            ";

            var grammar = new Pl0Grammar();
            var parser = new Parser(new LanguageData(grammar));
            var tree = parser.Parse(code);

            if (tree.HasErrors())
            {
                Console.WriteLine("Errores de compilación:");
                foreach (var msg in tree.ParserMessages)
                    Console.WriteLine($"[L{msg.Location.Line + 1}, C{msg.Location.Column + 1}] {msg.Message}");
                return;
            }

            var gen = new CodeGenerator();
            var asm = gen.Generate(tree.Root);

            Console.WriteLine("\n=== Ensamblador J1 ===\n");
            Console.WriteLine(asm);
            File.WriteAllText("out.j1.s", asm);

            var assembler = new AssemblerJ1();
            var result = assembler.Assemble(asm);
            File.WriteAllLines("out.j1.hex", result.HexLines);
            File.WriteAllText("out.j1.lst", result.Listing);

            Console.WriteLine("\nGenerado: out.j1.s, out.j1.hex, out.j1.lst");
        }
    }
}
