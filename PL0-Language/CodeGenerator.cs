using System;
using System.Linq;
using System.Collections.Generic;
using Irony.Parsing;

namespace PL0_Language.Codegen
{
    public sealed class CodeGenerator
    {
        private AsmEmitter A = null!;
        private SymbolTable Syms = null!;
        private bool _atTop;

        // Direcciones reservadas para frames
        private const int FRAME_PTR_ADDR = 0x0010; // [FRAME_PTR] = base del frame actual
        private const int FRAME_TOP_ADDR = 0x0011; // [FRAME_TOP] = siguiente libre
        private const int FRAME_HEAP_BASE = 0x0300; // zona de frames

        public string Generate(ParseTreeNode root)
        {
            A = new AsmEmitter();
            Syms = new SymbolTable();
            _atTop = true;

            // Saltar a main
            A.Emit("JUMP main");
            VisitProgram(root);

            // Stubs runtime
            A.Mark("write"); A.Comment("escribir T"); A.Emit("EXIT");
            A.Mark("read"); A.Comment("leer a T"); A.Emit("EXIT");
            A.Mark("mul"); A.Comment("multiplicación"); A.Emit("EXIT");
            A.Mark("div"); A.Comment("división"); A.Emit("EXIT");

            return A.GetText();
        }

        // ===== Helpers parse =====
        private static ParseTreeNode? ChildByName(ParseTreeNode n, string termName) =>
            n.ChildNodes.FirstOrDefault(c => string.Equals(c.Term.Name, termName, StringComparison.Ordinal));
        private static IEnumerable<ParseTreeNode> ChildrenByName(ParseTreeNode n, string termName) =>
            n.ChildNodes.Where(c => string.Equals(c.Term.Name, termName, StringComparison.Ordinal));
        private static bool IsToken(ParseTreeNode n, string termName) =>
            string.Equals(n.Term.Name, termName, StringComparison.Ordinal);
        private static string TokenText(ParseTreeNode n) => n.Token?.ValueString ?? n.Token?.Text ?? "";
        private static int TokenInt(ParseTreeNode n) => Convert.ToInt32(n.Token?.Value ?? 0);

        private static IEnumerable<ParseTreeNode> AllIdentifiers(ParseTreeNode n)
        {
            foreach (var c in n.ChildNodes)
            {
                if (c.Term.Name == "identifier") yield return c;
                foreach (var sub in AllIdentifiers(c)) yield return sub;
            }
        }
        private static IEnumerable<ParseTreeNode> AllNumbers(ParseTreeNode n)
        {
            foreach (var c in n.ChildNodes)
            {
                if (c.Term.Name == "number") yield return c;
                foreach (var sub in AllNumbers(c)) yield return sub;
            }
        }
        private static IEnumerable<ParseTreeNode> AllExpressions(ParseTreeNode n)
        {
            foreach (var c in n.ChildNodes)
            {
                if (c.Term.Name == "Expression") yield return c;
                foreach (var sub in AllExpressions(c)) yield return sub;
            }
        }

        // ===== Frames =====
        private void EmitFrameInit()
        {
            // [FRAME_TOP] := FRAME_HEAP_BASE ; [FRAME_PTR] := 0
            A.Emit($"LIT {FRAME_HEAP_BASE}");
            A.Emit($"LIT {FRAME_TOP_ADDR}"); A.Emit("!");
            A.Emit("LIT 0");
            A.Emit($"LIT {FRAME_PTR_ADDR}"); A.Emit("!");
        }

        private void AddrFromFrameOffset(int offset)
        {
            A.Emit($"LIT {FRAME_PTR_ADDR}");
            A.Emit("@");
            A.Emit($"LIT {offset}");
            A.Emit("ADD");
        }

        // ===== Recorrido =====
        private void VisitProgram(ParseTreeNode node)
        {
            var block = ChildByName(node, "Block") ?? node.ChildNodes.FirstOrDefault();
            if (block != null) VisitBlock(block);
        }

        private void VisitBlock(ParseTreeNode node)
        {
            var constOpt = ChildByName(node, "ConstDecl") ?? ChildByName(node, "ConstDeclOpt");
            var varOpt = ChildByName(node, "VarDecl") ?? ChildByName(node, "VarDeclOpt");
            var subrsOpt = ChildByName(node, "SubrDeclList") ?? ChildByName(node, "SubrDeclListOpt");
            var stmt = ChildByName(node, "Statement") ?? node.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");

            VisitConstOpt_Global(constOpt);
            VisitVarOpt_Global(varOpt);

            if (subrsOpt != null) VisitSubrDeclList(subrsOpt);

            if (_atTop)
            {
                A.Mark("main");
                EmitFrameInit();
                _atTop = false;
            }

            if (stmt != null) VisitStatement(stmt);
        }

        private void VisitConstOpt_Global(ParseTreeNode? node)
        {
            if (node == null || node.ChildNodes.Count == 0) return;

            var constDeclNodes = node.Term.Name == "ConstDecl"
                ? new[] { node }
                : node.ChildNodes.Where(c => c.Term.Name == "ConstDecl");

            bool found = false;
            foreach (var decl in constDeclNodes)
            {
                var ids = AllIdentifiers(decl).ToList();
                var nums = AllNumbers(decl).ToList();
                int k = Math.Min(ids.Count, nums.Count);
                for (int i = 0; i < k; i++)
                {
                    Syms.AddConst(TokenText(ids[i]), TokenInt(nums[i]), scope: "");
                    found = true;
                }
            }
            if (!found)
            {
                var ids = AllIdentifiers(node).ToList();
                var nums = AllNumbers(node).ToList();
                int k = Math.Min(ids.Count, nums.Count);
                for (int i = 0; i < k; i++)
                    Syms.AddConst(TokenText(ids[i]), TokenInt(nums[i]), scope: "");
            }
        }

        private void VisitVarOpt_Global(ParseTreeNode? node)
        {
            if (node == null || node.ChildNodes.Count == 0) return;
            var varDeclNodes = node.Term.Name == "VarDecl"
                ? new[] { node }
                : node.ChildNodes.Where(c => c.Term.Name == "VarDecl");

            var allIds = new List<string>();
            foreach (var decl in varDeclNodes)
                allIds.AddRange(AllIdentifiers(decl).Select(TokenText));

            if (allIds.Count == 0) allIds.AddRange(AllIdentifiers(node).Select(TokenText));

            foreach (var id in allIds) Syms.AddGlobalVar(id);
        }

        private void VisitSubrDeclList(ParseTreeNode node)
        {
            foreach (var sub in ChildrenByName(node, "SubrDecl")) VisitSubrDecl(sub);
            if (node.Term.Name == "SubrDecl") VisitSubrDecl(node);
            foreach (var ch in node.ChildNodes)
                if (ch.Term.Name == "SubrDeclList") VisitSubrDeclList(ch);
        }

        private void VisitSubrDecl(ParseTreeNode node)
        {
            var proc = ChildByName(node, "ProcDecl");
            var func = ChildByName(node, "FuncDecl");
            if (proc != null) VisitProc(proc);
            if (func != null) VisitFunc(func);
        }

        private static List<string> ReadParamNames(ParseTreeNode subrNode)
        {
            var plist = ChildByName(subrNode, "ParamListOpt");
            if (plist == null || plist.ChildNodes.Count == 0) return new List<string>();
            var list = ChildByName(plist, "ParamList");
            if (list == null) return new List<string>();
            return list.ChildNodes
                       .Where(ch => ch.Term.Name == "Param")
                       .Select(ch => TokenText(ch.ChildNodes.First(c => c.Term.Name == "identifier")))
                       .ToList();
        }

        private static List<string> ReadLocalVarNames(ParseTreeNode blockNode)
        {
            var varOpt = ChildByName(blockNode, "VarDecl") ?? ChildByName(blockNode, "VarDeclOpt");
            if (varOpt == null) return new List<string>();
            return AllIdentifiers(varOpt).Select(TokenText).ToList();
        }

        private void EmitPrologue(string subrName)
        {
            var (P, L) = Syms.FrameSizes(subrName);

            // base = [FRAME_TOP]
            A.Emit($"LIT {FRAME_TOP_ADDR}"); A.Emit("@");       // T=base

            // oldFP = [FRAME_PTR]; [base] = oldFP
            A.Emit("DUP");                                      // N=base, T=base
            A.Emit($"LIT {FRAME_PTR_ADDR}"); A.Emit("@");       // N=base, T=oldFP
            A.Emit("!");                                        // [base]=oldFP ; T=base

            // [FRAME_PTR] = base
            A.Emit($"LIT {FRAME_PTR_ADDR}"); A.Emit("SWAP"); A.Emit("!");

            // Copiar parámetros del último al primero: [FP+i] := pop()
            for (int i = P; i >= 1; i--)
            {
                AddrFromFrameOffset(i);
                A.Emit("!");
            }

            // FRAME_TOP = base + (1 + P + L)
            int frameSize = 1 + P + L;
            A.Emit($"LIT {FRAME_TOP_ADDR}"); A.Emit("@");   // recargar base
            A.Emit($"LIT {frameSize}"); A.Emit("ADD");
            A.Emit($"LIT {FRAME_TOP_ADDR}"); A.Emit("SWAP"); A.Emit("!");
        }

        private void EmitEpilogue(bool preserveTop)
        {
            if (preserveTop) A.Emit(">R"); // guardar retorno

            // base := [FRAME_PTR] ; oldFP := [base]
            A.Emit($"LIT {FRAME_PTR_ADDR}"); A.Emit("@"); // T=base
            A.Emit("DUP");                                // N=base, T=base
            A.Emit("@");                                  // N=base, T=oldFP

            // FRAME_TOP := base
            A.Emit("SWAP");                               // N=oldFP, T=base
            A.Emit($"LIT {FRAME_TOP_ADDR}"); A.Emit("SWAP"); A.Emit("!");

            // FRAME_PTR := oldFP
            A.Emit($"LIT {FRAME_PTR_ADDR}"); A.Emit("SWAP"); A.Emit("!");

            if (preserveTop) A.Emit("R>"); // recuperar retorno
            A.Emit("EXIT");
        }

        private void VisitProc(ParseTreeNode node)
        {
            var idNode = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
            var body = ChildByName(node, "Block") ?? node.ChildNodes.LastOrDefault();
            if (idNode == null || body == null) return;

            string name = TokenText(idNode);
            string lab = $"proc_{name}";
            A.Mark(lab);

            Syms.BeginSubprogram(name);

            foreach (var p in ReadParamNames(node)) Syms.AddParam(p);
            Syms.AddLocals(ReadLocalVarNames(body));

            EmitPrologue(name);
            VisitSubBlockBody(body);
            EmitEpilogue(preserveTop: false);

            Syms.EndSubprogram();
        }

        private void VisitFunc(ParseTreeNode node)
        {
            var idNode = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
            var body = ChildByName(node, "Block") ?? node.ChildNodes.LastOrDefault();
            if (idNode == null || body == null) return;

            string name = TokenText(idNode);
            string lab = $"func_{name}";
            A.Mark(lab);

            Syms.BeginSubprogram(name);

            foreach (var p in ReadParamNames(node)) Syms.AddParam(p);
            Syms.AddLocals(ReadLocalVarNames(body));

            EmitPrologue(name);
            bool hadReturn = VisitSubBlockBody(body, isFunction: true);
            if (!hadReturn) { A.Emit("LIT 0"); EmitEpilogue(preserveTop: true); }

            Syms.EndSubprogram();
        }

        private bool VisitSubBlockBody(ParseTreeNode blockNode, bool isFunction = false)
        {
            bool hadReturn = false;

            var constOpt = ChildByName(blockNode, "ConstDecl") ?? ChildByName(blockNode, "ConstDeclOpt");
            if (constOpt != null && constOpt.ChildNodes.Count > 0)
            {
                var ids = AllIdentifiers(constOpt).ToList();
                var nums = AllNumbers(constOpt).ToList();
                int k = Math.Min(ids.Count, nums.Count);
                for (int i = 0; i < k; i++)
                    Syms.AddConst(TokenText(ids[i]), TokenInt(nums[i]), scope: Syms.CurrentScope);
            }

            var subrsOpt = ChildByName(blockNode, "SubrDeclList") ?? ChildByName(blockNode, "SubrDeclListOpt");
            if (subrsOpt != null) VisitSubrDeclList(subrsOpt);

            var stmt = ChildByName(blockNode, "Statement") ?? blockNode.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");
            if (stmt != null) hadReturn = VisitStatement(stmt, insideFunction: isFunction) || hadReturn;

            return hadReturn;
        }

        // ===== Sentencias =====
        private bool VisitStatement(ParseTreeNode node, bool insideFunction = false)
        {
            if (node.ChildNodes.Count == 0) return false;
            bool hadReturn = false;

            var head = node.ChildNodes[0];

            if (IsToken(head, "identifier"))
            {
                var id = TokenText(head);
                var expr = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression")
                           ?? node.ChildNodes.LastOrDefault();
                if (expr == null) return false;

                EmitExpression(expr);

                if (Syms.TryGetOffset(id, out int ofs))
                {
                    AddrFromFrameOffset(ofs);
                    A.Emit("!");
                }
                else
                {
                    int addr = Syms.GetGlobalVarAddr(id);
                    A.Emit($"LIT {addr}");
                    A.Emit("!");
                }
                return false;
            }

            if (IsToken(head, "call"))
            {
                var nameId = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
                var args = ChildByName(node, "ArgListOpt");
                if (args != null) foreach (var e in AllExpressions(args)) EmitExpression(e);
                if (nameId != null) A.Emit($"CALL proc_{TokenText(nameId)}");
                return false;
            }

            if (IsToken(head, "begin"))
            {
                var list = ChildByName(node, "StatementList");
                if (list != null)
                    foreach (var st in ChildrenByName(list, "Statement"))
                        hadReturn = VisitStatement(st, insideFunction) || hadReturn;
                return hadReturn;
            }

            if (IsToken(head, "if"))
            {
                var cond = ChildByName(node, "Condition");
                var thenSt = node.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");
                if (cond == null || thenSt == null) return false;

                if (TryConstEq(cond, out bool eq)) { if (eq) hadReturn = VisitStatement(thenSt, insideFunction) || hadReturn; return hadReturn; }

                var L_then = A.NewLabel("THEN");
                var L_end = A.NewLabel("ENDI");

                EmitConditionEq(cond);              // 0 si verdadero
                A.Emit($"0BRANCH {L_then}");
                A.Emit($"JUMP {L_end}");
                A.Mark(L_then);
                hadReturn = VisitStatement(thenSt, insideFunction) || hadReturn;
                A.Mark(L_end);
                return hadReturn;
            }

            if (IsToken(head, "while"))
            {
                var cond = ChildByName(node, "Condition");
                var body = node.ChildNodes.LastOrDefault(c => c.Term.Name == "Statement");
                if (cond == null || body == null) return false;

                if (TryConstEq(cond, out bool eq2) && !eq2) return false;

                var L_top = A.NewLabel("WH");
                var L_body = A.NewLabel("WB");
                var L_exit = A.NewLabel("WE");

                A.Mark(L_top);
                EmitConditionEq(cond);           // 0 si verdadero
                A.Emit($"0BRANCH {L_body}");
                A.Emit($"JUMP {L_exit}");
                A.Mark(L_body);
                hadReturn = VisitStatement(body, insideFunction) || hadReturn;
                A.Emit($"JUMP {L_top}");
                A.Mark(L_exit);
                return hadReturn;
            }

            if (IsToken(head, "!"))
            {
                var expr = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression")
                           ?? node.ChildNodes.LastOrDefault();
                if (expr != null) { EmitExpression(expr); A.Emit("CALL write"); }
                return false;
            }

            if (IsToken(head, "?"))
            {
                var id = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
                if (id != null)
                {
                    A.Emit("CALL read");
                    var name = TokenText(id);
                    if (Syms.TryGetOffset(name, out int ofs)) { AddrFromFrameOffset(ofs); A.Emit("!"); }
                    else { int addr = Syms.GetGlobalVarAddr(name); A.Emit($"LIT {addr}"); A.Emit("!"); }
                }
                return false;
            }

            if (IsToken(head, "return"))
            {
                var expr = node.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression");
                if (expr != null) EmitExpression(expr); else A.Emit("LIT 0");
                EmitEpilogue(preserveTop: true);
                return true;
            }

            return false;
        }

        // ===== Condiciones / Expresiones =====
        private void EmitConditionEq(ParseTreeNode node)
        {
            var e = ChildrenByName(node, "Expression").ToList();
            if (e.Count == 2)
            {
                if (TryConstExpr(e[0], out var v1) && TryConstExpr(e[1], out var v2))
                { A.Emit($"LIT {(v1 == v2 ? 0 : 1)}"); return; }
                EmitExpression(e[0]);
                EmitExpression(e[1]);
                A.Emit("SUB"); // 0 si iguales
            }
            else
            {
                if (e.Count == 1) { EmitExpression(e[0]); } else A.Emit("LIT 1");
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
            if (n.Term.Name != "Expression")
            {
                if (n.Term.Name == "Term") { EmitTerm(n); return; }
            }

            if (n.ChildNodes.Count == 1 && n.ChildNodes[0].Term.Name == "Term")
            { EmitTerm(n.ChildNodes[0]); return; }

            if (TryConstExpr(n, out var cv)) { A.Emit($"LIT {cv}"); return; }

            var left = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Expression") ??
                        n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term");
            var opTok = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "+" || c.Term.Name == "-");
            var right = n.ChildNodes.LastOrDefault(c => c.Term.Name == "Term");

            if (left != null && opTok != null && right != null)
            {
                if (left.Term.Name == "Expression") EmitExpression(left); else EmitTerm(left);
                EmitTerm(right);
                if (opTok.Term.Name == "+") A.Emit("ADD"); else A.Emit("SUB");
                return;
            }

            var t = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Term");
            if (t != null) { EmitTerm(t); return; }
        }

        private void EmitTerm(ParseTreeNode n)
        {
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
                if (opTok.Term.Name == "*") A.Emit("CALL mul"); else A.Emit("CALL div");
                return;
            }

            var f = n.ChildNodes.FirstOrDefault(c => c.Term.Name == "Factor");
            if (f != null) EmitFactor(f);
        }

        private void EmitFactor(ParseTreeNode n)
        {
            var funCall = ChildByName(n, "FunCall");
            if (funCall != null)
            {
                var fid = funCall.ChildNodes.FirstOrDefault(c => c.Term.Name == "identifier");
                var args = ChildByName(funCall, "ArgListOptContent") ?? ChildByName(funCall, "ArgListOpt");
                if (args != null) foreach (var e in AllExpressions(args)) EmitExpression(e);
                if (fid != null) A.Emit($"CALL func_{TokenText(fid)}");
                return;
            }

            var id = ChildByName(n, "identifier");
            if (id != null)
            {
                var name = TokenText(id);
                var c = Syms.GetConst(name);
                if (c.HasValue) { A.Emit($"LIT {c.Value}"); return; }

                if (Syms.TryGetOffset(name, out int ofs))
                {
                    AddrFromFrameOffset(ofs);
                    A.Emit("@");
                    return;
                }

                int addr = Syms.GetGlobalVarAddr(name);
                A.Emit($"LIT {addr}");
                A.Emit("@");
                return;
            }

            var num = ChildByName(n, "number");
            if (num != null) { A.Emit($"LIT {TokenInt(num)}"); return; }

            var expr = ChildByName(n, "Expression");
            if (expr != null) { EmitExpression(expr); return; }

            if (n.Token != null)
            {
                if (n.Term.Name == "number") { A.Emit($"LIT {TokenInt(n)}"); return; }
                if (n.Term.Name == "identifier")
                {
                    var name = TokenText(n);
                    var c = Syms.GetConst(name);
                    if (c.HasValue) { A.Emit($"LIT {c.Value}"); return; }
                    if (Syms.TryGetOffset(name, out int ofs2)) { AddrFromFrameOffset(ofs2); A.Emit("@"); return; }
                    int addr = Syms.GetGlobalVarAddr(name);
                    A.Emit($"LIT {addr}"); A.Emit("@"); return;
                }
            }
            A.Emit("LIT 0");
        }

        private bool TryConstExpr(ParseTreeNode node, out int value)
        {
            value = 0;
            if (node.Term.Name == "Expression")
            {
                if (node.ChildNodes.Count == 1 && node.ChildNodes[0].Term.Name == "Term")
                    return TryConstTerm(node.ChildNodes[0], out value);

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
            return TryConstTerm(node, out value);
        }

        private bool TryConstTerm(ParseTreeNode node, out int value)
        {
            value = 0;
            if (node.Term.Name == "Term")
            {
                if (node.ChildNodes.Count == 1 && node.ChildNodes[0].Term.Name == "Factor")
                    return TryConstFactor(node.ChildNodes[0], out value);

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
                return false;
            }

            var expr = ChildByName(node, "Expression");
            if (expr != null) return TryConstExpr(expr, out value);

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
    }
}
