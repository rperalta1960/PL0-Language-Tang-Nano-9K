using System;
using Irony.Parsing;

namespace PL0_Language.Gramatica
{
    public class Pl0Grammar : Grammar
    {
        public Pl0Grammar()
        {
            // ===== Comentarios =====
            var blockComment = new CommentTerminal("block-comment", "(*", "*)");
            var lineComment = new CommentTerminal("line-comment", "//", "\n", "\r\n");
            NonGrammarTerminals.Add(blockComment);
            NonGrammarTerminals.Add(lineComment);

            // ===== Terminales =====
            var number = TerminalFactory.CreateCSharpNumber("number");
            var identifier = TerminalFactory.CreateCSharpIdentifier("identifier");

            // Literal de carácter con escapes comunes
            var charlit = new StringLiteral("charlit", "'", StringOptions.AllowsAllEscapes);

            // Símbolos / puntuación
            var semicolon = ToTerm(";");
            var period = ToTerm(".");
            var comma = ToTerm(",");
            var assign = ToTerm(":=");
            var equal = ToTerm("=");
            var colon = ToTerm(":");
            var plus = ToTerm("+");
            var minus = ToTerm("-");
            var mult = ToTerm("*");
            var div = ToTerm("/");
            var lparen = ToTerm("(");
            var rparen = ToTerm(")");
            var shl = ToTerm("<<");
            var shr = ToTerm(">>");

            // Palabras clave
            var kwConst = ToTerm("const");
            var kwVar = ToTerm("var");
            var kwProcedure = ToTerm("procedure");
            var kwFunction = ToTerm("function");
            var kwCall = ToTerm("call");
            var kwBegin = ToTerm("begin");
            var kwEnd = ToTerm("end");
            var kwIf = ToTerm("if");
            var kwThen = ToTerm("then");
            var kwElse = ToTerm("else");
            var kwWhile = ToTerm("while");
            var kwDo = ToTerm("do");
            var kwReturn = ToTerm("return");
            var kwInteger = ToTerm("integer");
            var kwChar = ToTerm("char");
            var kwWrite = ToTerm("!"); // salida
            var kwRead = ToTerm("?"); // entrada

            // Operadores bit a bit como palabras clave (unarios y binarios)
            var kwNot = ToTerm("not");
            var kwAnd = ToTerm("and");
            var kwNand = ToTerm("nand");
            var kwXor = ToTerm("xor");
            var kwNxor = ToTerm("nxor");
            var kwOr = ToTerm("or");
            var kwNor = ToTerm("nor");

            // ===== No terminales =====
            var Program = new NonTerminal("Program");
            var Block = new NonTerminal("Block");

            var ConstDeclOpt = new NonTerminal("ConstDeclOpt");
            var VarDeclOpt = new NonTerminal("VarDeclOpt");
            var SubrDeclListOpt = new NonTerminal("SubrDeclListOpt");

            var ConstDecl = new NonTerminal("ConstDecl");
            var ConstList = new NonTerminal("ConstList");

            var VarDecl = new NonTerminal("VarDecl");
            var IdList = new NonTerminal("IdList");

            var SubrDeclList = new NonTerminal("SubrDeclList");
            var SubrDecl = new NonTerminal("SubrDecl");

            var ProcDecl = new NonTerminal("ProcDecl");
            var FuncDecl = new NonTerminal("FuncDecl");

            var ParamListOpt = new NonTerminal("ParamListOpt");
            var ParamList = new NonTerminal("ParamList");
            var Param = new NonTerminal("Param");
            var Type = new NonTerminal("Type");

            var Statement = new NonTerminal("Statement");
            var StatementList = new NonTerminal("StatementList");
            var Return = new NonTerminal("Return");

            var Call = new NonTerminal("Call");
            var ArgListOpt = new NonTerminal("ArgListOpt");          // con paréntesis (CALL)
            var ArgList = new NonTerminal("ArgList");
            var ArgListOptContent = new NonTerminal("ArgListOptContent");   // SIN paréntesis (FunCall)

            // Expresiones con niveles de precedencia
            var Expression = new NonTerminal("Expression");  // OrExpr
            var OrExpr = new NonTerminal("OrExpr");
            var XorExpr = new NonTerminal("XorExpr");
            var AndExpr = new NonTerminal("AndExpr");
            var AddExpr = new NonTerminal("AddExpr");
            var MulExpr = new NonTerminal("MulExpr");
            var ShiftExpr = new NonTerminal("ShiftExpr");
            var ShiftCount = new NonTerminal("ShiftCount");
            var UnaryExpr = new NonTerminal("UnaryExpr");
            var Primary = new NonTerminal("Primary");

            var FunCall = new NonTerminal("FunCall");
            var Condition = new NonTerminal("Condition");

            // ===== Reglas =====
            Root = Program;

            Program.Rule = Block + period;

            Block.Rule = ConstDeclOpt + VarDeclOpt + SubrDeclListOpt + Statement;

            // Constantes y variables
            ConstDeclOpt.Rule = Empty | ConstDecl;
            ConstDecl.Rule = kwConst + ConstList + semicolon;
            ConstList.Rule = MakePlusRule(ConstList, comma, identifier + equal + number);

            VarDeclOpt.Rule = Empty | VarDecl;
            VarDecl.Rule = kwVar + IdList + semicolon;
            IdList.Rule = MakePlusRule(IdList, comma, identifier);

            // Subprogramas
            SubrDeclListOpt.Rule = Empty | SubrDeclList;
            SubrDeclList.Rule = MakePlusRule(SubrDeclList, SubrDecl);
            SubrDecl.Rule = ProcDecl | FuncDecl;

            // Parámetros y tipos
            ParamListOpt.Rule = Empty | lparen + ParamList + rparen;
            ParamList.Rule = MakePlusRule(ParamList, comma, Param);
            Param.Rule = identifier + colon + Type;
            Type.Rule = kwInteger | kwChar;

            // Procedimiento y función
            ProcDecl.Rule = kwProcedure + identifier + ParamListOpt + semicolon + Block + semicolon;
            FuncDecl.Rule = kwFunction + identifier + ParamListOpt + colon + Type + semicolon + Block + semicolon;

            // Llamadas y argumentos
            ArgList.Rule = MakePlusRule(ArgList, comma, Expression);
            ArgListOpt.Rule = Empty | lparen + ArgList + rparen;     // para 'call'
            ArgListOptContent.Rule = Empty | ArgList;                        // para 'FunCall' (SIN paréntesis)

            Call.Rule = kwCall + identifier + ArgListOpt;

            // Sentencias (incluye return, I/O, if con else)
            Statement.Rule = identifier + assign + Expression
                           | Call
                           | kwBegin + StatementList + kwEnd
                           | kwIf + Condition + kwThen + Statement + kwElse + Statement
                           | kwIf + Condition + kwThen + Statement
                           | kwWhile + Condition + kwDo + Statement
                           | kwWrite + Expression
                           | kwRead + identifier
                           | Return
                           | Empty;

            Return.Rule = kwReturn + Expression;
            StatementList.Rule = MakePlusRule(StatementList, semicolon, Statement);

            // ===== Expresiones con precedencia =====
            Expression.Rule = OrExpr;

            OrExpr.Rule = OrExpr + kwOr + XorExpr
                         | OrExpr + kwNor + XorExpr
                         | XorExpr;

            XorExpr.Rule = XorExpr + kwXor + AndExpr
                         | XorExpr + kwNxor + AndExpr
                         | AndExpr;

            AndExpr.Rule = AndExpr + kwAnd + AddExpr
                         | AndExpr + kwNand + AddExpr
                         | AddExpr;

            AddExpr.Rule = AddExpr + plus + MulExpr
                         | AddExpr + minus + MulExpr
                         | MulExpr;

            MulExpr.Rule = MulExpr + mult + ShiftExpr
                         | MulExpr + div + ShiftExpr
                         | ShiftExpr;

            ShiftExpr.Rule = ShiftExpr + shl + ShiftCount
                           | ShiftExpr + shr + ShiftCount
                           | UnaryExpr;

            ShiftCount.Rule = number | identifier;   // permite usar CONSTs como n1, n4, n15


            UnaryExpr.Rule = kwNot + UnaryExpr
                           | Primary;

            // Primary (átomos)
            FunCall.Rule = identifier + PreferShiftHere() + lparen + ArgListOptContent + rparen;

            Primary.Rule = FunCall
                         | identifier
                         | number
                         | charlit
                         | lparen + Expression + rparen;

            // Condición (igualdad)
            Condition.Rule = Expression + equal + Expression;

            // ===== Decorado léxico =====
            MarkReservedWords("const", "var", "procedure", "function", "call", "begin", "end",
                              "if", "then", "else", "while", "do", "return", "integer", "char",
                              "not", "and", "nand", "xor", "nxor", "or", "nor");

            // Puntuación y operadores
            MarkPunctuation(";", ",", ":=", "=", ".", "(", ")", ":", "<<", ">>");
            // (Registro de operadores, opcional si ya hay precedencia por reglas)
            RegisterOperators(1, "not");
            RegisterOperators(2, "<<", ">>");
            RegisterOperators(3, "*", "/");
            RegisterOperators(4, "+", "-");
            RegisterOperators(5, "and", "nand");
            RegisterOperators(6, "xor", "nxor");
            RegisterOperators(7, "or", "nor");
        }
    }
}

