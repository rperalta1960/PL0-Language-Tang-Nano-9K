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
                // ======== Programa PL/0 de prueba total ========
                // Ejercita: operadores bit a bit, corrimientos,
                // literales char, if-else, procedimientos,
                // funciones recursivas y E/S.

                const a = 0x00F0, b = 0x0F0F, n1 = 1, n4 = 4, n15 = 15;
                var
                  r_or, r_nor, r_and, r_nand, r_xor, r_nxor, r_not,
                  r_shl1, r_shl15, r_shr4,
                  ch, out1, out2, fa;

                procedure show(label: integer, value: integer);
                begin
                    !label; !value  // imprime etiqueta y valor
                end;

                function id_char(c: char): integer;
                begin
                    return c  // devuelve el código del carácter
                end;

                function isA(c: char): integer;
                begin
                    (* if-else clásico: devuelve 1 si es 'A', de lo contrario 0 *)
                    if c = 'A' then
                        return 1
                    else
                        return 0
                end;

                function fact(x: integer): integer;
                begin
                    // recursión: factorial
                    if x = 0 then
                        return 1
                    else
                        return x * fact(x - 1)
                end;

                begin
                    // --- Operadores bit a bit ---
                    r_or   := a or b;
                    r_nor  := a nor b;
                    r_and  := a and b;
                    r_nand := a nand b;
                    r_xor  := a xor b;
                    r_nxor := a nxor b;
                    r_not  := not a;

                    // --- Corrimientos (1..15) ---
                    r_shl1  := a << n1;
                    r_shl15 := a << n15;
                    r_shr4  := b >> n4;

                    // --- Literales y parámetros char ---
                    ch   := id_char('Z');
                    out1 := isA('A');   // 1
                    out2 := isA('Z');   // 0

                    // --- Recursión (factorial de 5) ---
                    fa := fact(5);      // 120

                    // --- Mostrar resultados ---
                    call show(1001, r_or);
                    call show(1002, r_nor);
                    call show(1003, r_and);
                    call show(1004, r_nand);
                    call show(1005, r_xor);
                    call show(1006, r_nxor);
                    call show(1007, r_not);

                    call show(1011, r_shl1);
                    call show(1015, r_shl15);
                    call show(1040, r_shr4);

                    call show(2001, ch);
                    call show(2002, out1);
                    call show(2003, out2);

                    call show(3001, fa)
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
