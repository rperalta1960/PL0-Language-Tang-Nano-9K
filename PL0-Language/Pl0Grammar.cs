using System;
using Irony.Parsing;

namespace PL0_Language.Gramatica
{
    public class Pl0Grammar : Grammar
    {
        public Pl0Grammar()
        {
            // Comentarios
            var blockComment = new CommentTerminal("block-comment", "(*", "*)");
            var lineComment = new CommentTerminal("line-comment", "//", "\n", "\r\n");
            NonGrammarTerminals.Add(blockComment);
            NonGrammarTerminals.Add(lineComment);

            // Terminales
            var number = TerminalFactory.CreateCSharpNumber("number");
            var identifier = TerminalFactory.CreateCSharpIdentifier("identifier");

            // Símbolos
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
            var kwWhile = ToTerm("while");
            var kwDo = ToTerm("do");
            var kwReturn = ToTerm("return");
            var kwInteger = ToTerm("integer");
            var kwWrite = ToTerm("!");
            var kwRead = ToTerm("?");
            var kwElse = ToTerm("else");


            // No terminales
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
            var ArgListOpt = new NonTerminal("ArgListOpt");         // (args) | ε
            var ArgList = new NonTerminal("ArgList");
            var ArgListOptContent = new NonTerminal("ArgListOptContent");  // args | ε (sin paréntesis)
            var Expression = new NonTerminal("Expression");
            var Term = new NonTerminal("Term");
            var Factor = new NonTerminal("Factor");
            var FunCall = new NonTerminal("FunCall");
            var Condition = new NonTerminal("Condition");

            // Reglas
            Root = Program;

            Program.Rule = Block + period;
            Block.Rule = ConstDeclOpt + VarDeclOpt + SubrDeclListOpt + Statement;

            ConstDeclOpt.Rule = Empty | ConstDecl;
            ConstDecl.Rule = kwConst + ConstList + semicolon;
            ConstList.Rule = MakePlusRule(ConstList, comma, identifier + equal + number);

            VarDeclOpt.Rule = Empty | VarDecl;
            VarDecl.Rule = kwVar + IdList + semicolon;
            IdList.Rule = MakePlusRule(IdList, comma, identifier);

            SubrDeclListOpt.Rule = Empty | SubrDeclList;
            SubrDeclList.Rule = MakePlusRule(SubrDeclList, SubrDecl);
            SubrDecl.Rule = ProcDecl | FuncDecl;

            ParamListOpt.Rule = Empty | lparen + ParamList + rparen;
            ParamList.Rule = MakePlusRule(ParamList, comma, Param);
            Param.Rule = identifier + colon + Type;
            Type.Rule = kwInteger;

            ProcDecl.Rule = kwProcedure + identifier + ParamListOpt + semicolon + Block + semicolon;
            FuncDecl.Rule = kwFunction + identifier + ParamListOpt + colon + Type + semicolon + Block + semicolon;

            ArgList.Rule = MakePlusRule(ArgList, comma, Expression);
            ArgListOpt.Rule = Empty | lparen + ArgList + rparen;    // para CALL
            ArgListOptContent.Rule = Empty | ArgList;                       // para FunCall

            Call.Rule = kwCall + identifier + ArgListOpt;

            Statement.Rule = identifier + assign + Expression
                           | Call
                           | kwBegin + StatementList + kwEnd
                           // ← alternativa con ELSE primero
                           | kwIf + Condition + kwThen + Statement + kwElse + Statement
                           | kwIf + Condition + kwThen + Statement
                           | kwWhile + Condition + kwDo + Statement
                           | kwWrite + Expression
                           | kwRead + identifier
                           | Return
                           | Empty;

            Return.Rule = kwReturn + Expression;
            StatementList.Rule = MakePlusRule(StatementList, semicolon, Statement);

            Expression.Rule = Expression + plus + Term
                            | Expression + minus + Term
                            | Term;

            Term.Rule = Term + mult + Factor
                      | Term + div + Factor
                      | Factor;

            FunCall.Rule = identifier + PreferShiftHere() + lparen + ArgListOptContent + rparen;
            Factor.Rule = FunCall | identifier | number | lparen + Expression + rparen;

            Condition.Rule = Expression + equal + Expression;

            // Decorado
            MarkReservedWords("const", "var", "procedure", "function", "call", "begin", "end",
                  "if", "then", "else", "while", "do", "return", "integer");

            MarkPunctuation(";", ",", ":=", "=", ".", "(", ")", ":");
            RegisterOperators(1, "+", "-");
            RegisterOperators(2, "*", "/");
        }
    }
}
