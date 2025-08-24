// CodeGenerator.cs
using System;
using System.Linq;
using System.Collections.Generic;
using Irony.Parsing;

namespace PL0_Language.Codegen
{
    /// <summary>
    /// Generador de ensamblador J1 desde el ParseTree de Irony para PL/0.
    /// - Seguro ante nodos opcionales (no indexa por posiciones fijas).
    /// - Plegado de constantes: +, -, *, / y condiciones (==).
    /// - Emite mnemónicos J1 que entiende AssemblerJ1 (LIT, JUMP, 0BRANCH, CALL, ADD, SUB, @, !, EXIT, etc.).
    /// </summary>
    public sealed class CodeGenerator
    {
        private AsmEmitter A = null!;
        private SymbolTable Syms = null!;
        private bool _atTop;

        public string Generate(ParseTreeNode root)
        {
            A = new AsmEmitter();
            Syms = new SymbolTable();
            _atTop = true;

            // Prefacio: saltar a main
            A.Emit("JUMP main");

            VisitProgram(root);

            // Stubs de runtime (ajusta/implementa según tu plataforma)
            A.Mark("write"); A.Comment("escribir T"); A.Emit("EXIT");
            A.Mark("read"); A.Comment("leer a T"); A.Emit("EXIT");
            A.Mark("mul"); A.Comment("multiplicación"); A.Emit("EXIT");
            A.Mark("div"); A.Comment("división"); A.Emit("EXIT");

            return A.GetText();
        }

        // ==========================
        // Helpers de navegación segura
        // ==========================

        private static ParseTreeNode? ChildByName(ParseTreeNode n, string termName) =>
            n.ChildNodes.FirstOrDefault(c => string.Equals(c.Term.Name, termName, StringComparison.Ordinal));

        private static IEnumerable<ParseTreeNode> ChildrenByName(ParseTreeNode n, string termName) =>
            n.ChildNodes.Where(c => string.Equals(c.Term.Name, termName, StringComparison.Ordinal));

        private static bool IsToken(ParseTreeNode n, string termName) =>
            string.Equals(n.Term.Name, termName, StringComparison.Ordinal);

        private static string TokenText(ParseTreeNode n) => n.Token?.ValueString ?? n.Token?.Text ?? "";

        private static int TokenInt(ParseTreeNode n) => Convert.ToInt32(n.Token?.Value ?? 0);

        // ==========================
        // Recorrido principal
        // ==========================

        private void VisitProgram(ParseTreeNode node)
        {
            // Esperado: Program -> Block .
            var block = ChildByName(node, "Block") ?? node.ChildNodes.FirstOrDefault();
            if (block != null) VisitBlock(block);
        }

        private void VisitBlock(ParseTreeNode node)
        {
            // Block -> ConstDeclOpt VarDeclOpt ProcDeclList Statement
            // Cada uno puede estar vacío; tratarlos por nombre y con null-check
            var constOpt = ChildByName(node, "ConstDecl") ?? ChildByName(node, "ConstDeclOpt");
            var varOpt = ChildByName(node, "VarDecl") ?? ChildByName(node, "VarDeclOpt");
            var procs = ChildByName(node, "ProcDeclList");
            var stmt = ChildByName(node, "Statement") ?? node.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");

            VisitConstOpt(constOpt);
            VisitVarOpt(varOpt);

            if (procs != null) VisitProcList(procs);

            if (_atTop) { A.Mark("main"); _atTop = false; }

            if (stmt != null) VisitStatement(stmt);
        }

        // ==========================
        // Declaraciones
        // ==========================

        //private void VisitConstOpt(ParseTreeNode? node)
        //{
        //    if (node == null || node.ChildNodes.Count == 0) return;

        //    // Puede venir envuelto: tomar primer identifier y number
        //    var idNode = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
        //    var numNode = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "number");

        //    if (idNode != null && numNode != null)
        //        Syms.AddConst(TokenText(idNode), TokenInt(numNode));

        //    // Si tu gramática permite varias const separadas por comas, extiende aquí el bucle.
        //}

        //private void VisitVarOpt(ParseTreeNode? node)
        //{
        //    if (node == null || node.ChildNodes.Count == 0) return;

        //    // VarDecl -> 'var' identifier { "," identifier } ";"
        //    var idNodes = node.ChildNodes.Where(c => c.Term.Name == "identifier");
        //    foreach (var idNode in idNodes)
        //        Syms.AddVar(TokenText(idNode));
        //}

        // VarDecl y ConstDecl robustos (listas con comas, múltiples declaraciones)

        // Busca recursivamente todos los identificadores bajo un nodo
        private static IEnumerable<ParseTreeNode> AllIdentifiers(ParseTreeNode n)
        {
            foreach (var c in n.ChildNodes)
            {
                if (c.Term.Name == "identifier") yield return c;
                foreach (var sub in AllIdentifiers(c)) yield return sub;
            }
        }

        // Busca recursivamente todos los números bajo un nodo
        private static IEnumerable<ParseTreeNode> AllNumbers(ParseTreeNode n)
        {
            foreach (var c in n.ChildNodes)
            {
                if (c.Term.Name == "number") yield return c;
                foreach (var sub in AllNumbers(c)) yield return sub;
            }
        }

        // ConstDeclOpt: admite una o varias const (según tu gramática)
        private void VisitConstOpt(ParseTreeNode? node)
        {
            if (node == null || node.ChildNodes.Count == 0) return;

            // Dos posibilidades comunes:
            //  a) nodo == ConstDecl
            //  b) nodo envolviendo uno o más ConstDecl hijos
            var constDeclNodes = node.Term.Name == "ConstDecl"
                ? new[] { node }
                : node.ChildNodes.Where(c => c.Term.Name == "ConstDecl");

            bool found = false;
            foreach (var decl in constDeclNodes)
            {
                // Patrones típicos:
                //   const identifier = number ;
                //   const identifier = number , identifier = number , ... ;
                // Recolectamos pares id = num dentro del decl:
                var ids = AllIdentifiers(decl).ToList();
                var nums = AllNumbers(decl).ToList();

                // Emparejar por orden de aparición: id0=nums0, id1=nums1, ...
                int pairs = Math.Min(ids.Count, nums.Count);
                for (int i = 0; i < pairs; i++)
                {
                    Syms.AddConst(TokenText(ids[i]), TokenInt(nums[i]));
                    found = true;
                }
            }

            // Caso: algunas gramáticas ponen directamente identifier y number en ConstDeclOpt
            if (!found)
            {
                var ids = AllIdentifiers(node).ToList();
                var nums = AllNumbers(node).ToList();
                int pairs = Math.Min(ids.Count, nums.Count);
                for (int i = 0; i < pairs; i++)
                    Syms.AddConst(TokenText(ids[i]), TokenInt(nums[i]));
            }
        }

        // VarDeclOpt: admite una o varias vars separadas por coma
        private void VisitVarOpt(ParseTreeNode? node)
        {
            if (node == null || node.ChildNodes.Count == 0) return;

            // Dos posibilidades:
            //  a) nodo == VarDecl
            //  b) nodo envolviendo uno o más VarDecl hijos
            var varDeclNodes = node.Term.Name == "VarDecl"
                ? new[] { node }
                : node.ChildNodes.Where(c => c.Term.Name == "VarDecl");

            var allIds = new List<string>();
            foreach (var decl in varDeclNodes)
                allIds.AddRange(AllIdentifiers(decl).Select(TokenText));

            // Si no encontramos VarDecl explícitos (algunas gramáticas aplanan),
            // busca identifiers directamente bajo node.
            if (allIds.Count == 0)
                allIds.AddRange(AllIdentifiers(node).Select(TokenText));

            Syms.AddVars(allIds);
        }


        private void VisitProcList(ParseTreeNode node)
        {
            // Listas recursivas o planas: visitar cualquier ProcDecl encontrado
            foreach (var p in ChildrenByName(node, "ProcDecl"))
                VisitProc(p);

            // Si el propio nodo ya es ProcDecl
            if (node.Term.Name == "ProcDecl")
                VisitProc(node);

            // Profundizar en sublistas
            foreach (var ch in node.ChildNodes)
                if (ch.Term.Name == "ProcDeclList")
                    VisitProcList(ch);
        }

        private void VisitProc(ParseTreeNode node)
        {
            // ProcDecl -> 'procedure' identifier ';' Block ';'
            var idNode = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
            var body = ChildByName(node, "Block") ?? node.ChildNodes.LastOrDefault();
            if (idNode == null || body == null) return;

            var lab = $"proc_{TokenText(idNode)}";
            A.Mark(lab);
            VisitBlock(body);
            A.Emit("EXIT"); // return
        }

        // ==========================
        // Sentencias
        // ==========================

        private void VisitStatement(ParseTreeNode node)
        {
            if (node.ChildNodes.Count == 0) return;

            var head = node.ChildNodes[0];

            // identifier ':=' Expression     (asignación)
            if (IsToken(head, "identifier"))
            {
                var id = TokenText(head);
                var expr = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression")
                           ?? node.ChildNodes.LastOrDefault();
                if (expr == null) return;

                EmitExpression(expr);            // deja valor en T

                Console.WriteLine("--- SYMTAB ---\n" + Syms.DebugDump() + "\n-------------");


                int addr = Syms.GetVarAddr(id);  // valida que sea var
                A.Emit($"LIT {addr}");
                A.Emit("!");
                return;
            }

            // call identifier
            if (IsToken(head, "call"))
            {
                var pid = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
                if (pid != null) A.Emit($"CALL proc_{TokenText(pid)}");
                return;
            }

            // begin StatementList end
            if (IsToken(head, "begin"))
            {
                var list = ChildByName(node, "StatementList");
                if (list != null)
                {
                    foreach (var st in ChildrenByName(list, "Statement"))
                        VisitStatement(st);
                }
                return;
            }

            // if Condition then Statement
            if (IsToken(head, "if"))
            {
                var cond = ChildByName(node, "Condition");
                var thenSt = node.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");
                if (cond == null || thenSt == null) return;

                if (TryConstEq(cond, out bool eq))
                { if (eq) VisitStatement(thenSt); return; }

                var L_then = A.NewLabel("THEN");
                var L_end = A.NewLabel("ENDI");

                // Convención: deja 0 si E1 == E2
                EmitConditionEq(cond);
                A.Emit($"0BRANCH {L_then}"); // salta si 0 (verdadero igualdad)
                A.Emit($"JUMP {L_end}");
                A.Mark(L_then);
                VisitStatement(thenSt);
                A.Mark(L_end);
                return;
            }

            // while Condition do Statement
            if (IsToken(head, "while"))
            {
                var cond = ChildByName(node, "Condition");
                var body = node.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");
                if (cond == null || body == null) return;

                // Si la condición es constante y falsa, elimina el bucle
                if (TryConstEq(cond, out bool eq2) && !eq2) return;

                var L_top = A.NewLabel("WH");
                var L_body = A.NewLabel("WB");
                var L_exit = A.NewLabel("WE");

                A.Mark(L_top);
                EmitConditionEq(cond);           // 0 si verdadero (E1==E2)
                A.Emit($"0BRANCH {L_body}");
                A.Emit($"JUMP {L_exit}");
                A.Mark(L_body);
                VisitStatement(body);
                A.Emit($"JUMP {L_top}");
                A.Mark(L_exit);
                return;
            }

            // ! Expression  (write)
            if (IsToken(head, "!"))
            {
                var expr = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression")
                           ?? node.ChildNodes.LastOrDefault();
                if (expr != null)
                {
                    EmitExpression(expr);
                    A.Emit("CALL write");
                }
                return;
            }

            // ? identifier  (read)
            if (IsToken(head, "?"))
            {
                var id = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
                if (id != null)
                {
                    A.Emit("CALL read");                   // deja valor en T
                    int addr = Syms.GetVarAddr(TokenText(id));
                    A.Emit($"LIT {addr}");                 // T=addr, N=valor
                    A.Emit("!");
                }
                return;
            }

            // Otros/epsilon: no emitir nada
        }

        // ==========================
        // Condiciones y expresiones
        // ==========================

        // Condition -> Expression '=' Expression
        // Emite 0 si E1 == E2 (usamos SUB: N - T)
        private void EmitConditionEq(ParseTreeNode node)
        {
            var exprs = ChildrenByName(node, "Expression").ToList();
            if (exprs.Count == 2)
            {
                if (TryConstExpr(exprs[0], out var v1) && TryConstExpr(exprs[1], out var v2))
                {
                    A.Emit($"LIT {(v1 == v2 ? 0 : 1)}");
                    return;
                }

                EmitExpression(exprs[0]);
                EmitExpression(exprs[1]);
                A.Emit("SUB"); // N - T; si iguales => 0
            }
            else
            {
                // fallback: si no detectó 2 expresiones, emitir 1 (no ideal) para no reventar
                if (exprs.Count == 1) { EmitExpression(exprs[0]); }
                else A.Emit("LIT 1");
            }
        }

        private bool TryConstEq(ParseTreeNode node, out bool eq)
        {
            var e = ChildrenByName(node, "Expression").ToList();
            if (e.Count == 2 && TryConstExpr(e[0], out var v1) && TryConstExpr(e[1], out var v2))
            { eq = (v1 == v2); return true; }
            eq = false; return false;
        }

        private void EmitExpression(ParseTreeNode n)
        {
            // Muchos árboles vienen como:
            //   Expression -> Term
            //   Expression -> Expression (+|-) Term
            if (n.Term.Name != "Expression")
            {
                if (n.Term.Name == "Term") { EmitTerm(n); return; }
            }

            // Caso simple: un único Term
            if (n.ChildNodes.Count == 1 && n.ChildNodes[0].Term.Name == "Term")
            { EmitTerm(n.ChildNodes[0]); return; }

            // Plegado
            if (TryConstExpr(n, out var cv)) { A.Emit($"LIT {cv}"); return; }

            // Patrón genérico: [Expression/Term] op [Term]
            var left = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression") ??
                        n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term");
            var opTok = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "+" || c.Term.Name == "-");
            var right = n.ChildNodes.LastOrDefault(c => c.Term.Name == "Term");

            if (left != null && opTok != null && right != null)
            {
                // Emitir lado izquierdo
                if (left.Term.Name == "Expression") EmitExpression(left);
                else EmitTerm(left);

                // Emitir derecho (Term)
                EmitTerm(right);

                if (opTok.Term.Name == "+") A.Emit("ADD");
                else A.Emit("SUB");
                return;
            }

            // Fallback: si hay algún Term, emitirlo
            var t = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term");
            if (t != null) { EmitTerm(t); return; }
        }

        private void EmitTerm(ParseTreeNode n)
        {
            // Term -> Factor
            // Term -> Term (*|/) Factor
            if (n.ChildNodes.Count == 1 && n.ChildNodes[0].Term.Name == "Factor")
            { EmitFactor(n.ChildNodes[0]); return; }

            if (TryConstTerm(n, out var cv)) { A.Emit($"LIT {cv}"); return; }

            var left = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term") ??
                        n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Factor");
            var opTok = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "*" || c.Term.Name == "/");
            var right = n.ChildNodes.LastOrDefault(c => c.Term.Name == "Factor");

            if (left != null && opTok != null && right != null)
            {
                if (left.Term.Name == "Term") EmitTerm(left); else EmitFactor(left);
                EmitFactor(right);

                if (opTok.Term.Name == "*") A.Emit("CALL mul");
                else A.Emit("CALL div");
                return;
            }

            // Fallback
            var f = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Factor");
            if (f != null) EmitFactor(f);
        }

        private void EmitFactor(ParseTreeNode n)
        {
            // Factor ::= identifier | number | '(' Expression ')'
            var id = ChildByName(n, "identifier");
            if (id != null)
            {
                var name = TokenText(id);
                var c = Syms.GetConst(name);
                if (c.HasValue) { A.Emit($"LIT {c.Value}"); return; }

                int addr = Syms.GetVarAddr(name);
                A.Emit($"LIT {addr}");
                A.Emit("@");
                return;
            }

            var num = ChildByName(n, "number");
            if (num != null) { A.Emit($"LIT {TokenInt(num)}"); return; }

            // Paréntesis
            var expr = ChildByName(n, "Expression");
            if (expr != null) { EmitExpression(expr); return; }

            // Fallback si Irony encapsuló el token directo
            if (n.Token != null)
            {
                if (n.Term.Name == "number") { A.Emit($"LIT {TokenInt(n)}"); return; }
                if (n.Term.Name == "identifier")
                {
                    var name = TokenText(n);
                    var c = Syms.GetConst(name);
                    if (c.HasValue) { A.Emit($"LIT {c.Value}"); return; }
                    int addr = Syms.GetVarAddr(name);
                    A.Emit($"LIT {addr}"); A.Emit("@"); return;
                }
            }

            // Si no se reconoce nada, empuja 0 como neutro (evitar fallos en árboles atípicos)
            A.Emit("LIT 0");
        }

        // ==========================
        // Plegado de constantes
        // ==========================

        private bool TryConstExpr(ParseTreeNode node, out int value)
        {
            value = 0;

            if (node.Term.Name == "Expression")
            {
                // Expression -> Term
                if (node.ChildNodes.Count == 1 && node.ChildNodes[0].Term.Name == "Term")
                    return TryConstTerm(node.ChildNodes[0], out value);

                // Expression -> Expression (+|-) Term
                var left = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression");
                var opTok = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "+" || c.Term.Name == "-");
                var right = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term");

                if (left != null && opTok != null && right != null &&
                    TryConstExpr(left, out var l) && TryConstTerm(right, out var r))
                {
                    value = (opTok.Term.Name == "+") ? (l + r) : (l - r);
                    return true;
                }
            }

            // Delegar a Term/Factor si vino etiquetado distinto
            return TryConstTerm(node, out value);
        }

        private bool TryConstTerm(ParseTreeNode node, out int value)
        {
            value = 0;

            if (node.Term.Name == "Term")
            {
                // Term -> Factor
                if (node.ChildNodes.Count == 1 && node.ChildNodes[0].Term.Name == "Factor")
                    return TryConstFactor(node.ChildNodes[0], out value);

                // Term -> Term (*|/) Factor
                var left = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term");
                var opTok = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "*" || c.Term.Name == "/");
                var right = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Factor");

                if (left != null && opTok != null && right != null &&
                    TryConstTerm(left, out var l) && TryConstFactor(right, out var r))
                {
                    if (opTok.Term.Name == "*") { value = l * r; return true; }
                    if (opTok.Term.Name == "/") { value = (r == 0) ? 0 : (l / r); return true; }
                }
            }

            // Delegar a Factor si vino etiquetado distinto
            return TryConstFactor(node, out value);
        }

        private bool TryConstFactor(ParseTreeNode node, out int value)
        {
            value = 0;

            var num = ChildByName(node, "number");
            if (num != null) { value = TokenInt(num); return true; }

            var id = ChildByName(node, "identifier");
            if (id != null)
            {
                var name = TokenText(id);
                var c = Syms.GetConst(name);
                if (c.HasValue) { value = c.Value; return true; }
                return false; // es variable -> no constante
            }

            // '(' Expression ')'
            var expr = ChildByName(node, "Expression");
            if (expr != null) return TryConstExpr(expr, out value);

            // Fallback a token directo
            if (node.Token != null)
            {
                if (node.Term.Name == "number") { value = TokenInt(node); return true; }
                if (node.Term.Name == "identifier")
                {
                    var name = TokenText(node);
                    var c = Syms.GetConst(name);
                    if (c.HasValue) { value = c.Value; return true; }
                }
            }

            return false;
        }

        // ==========================
        // (Opcional) Dumper del árbol
        // ==========================
        private static void Dump(ParseTreeNode n, int d = 0)
        {
            Console.WriteLine($"{new string(' ', d * 2)}- {n.Term.Name}" + (n.Token != null ? $" : '{n.Token.Text}'" : ""));
            foreach (var c in n.ChildNodes) Dump(c, d + 1);
        }
    }
}
