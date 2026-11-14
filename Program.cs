using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CToNasm
{
    internal class Program
    {
        static readonly Dictionary<string, string> Examples =
            new Dictionary<string, string>
            {
                ["basic"] = """
int main() {
    int a = 12;
    int b = 30;
    int sum = a + b;

    printf("Hello from Standalone NASM!\n");
    printf("Value of a = %d, b = %d\n", a, b);
    printf("Total sum is: %d\n", sum);

    return 0;
}
""",
                ["fibonacci"] = """
int main() {
    printf("Fibonacci Sequence:\n");
    int n = 10;
    int t1 = 0;
    int t2 = 1;

    for (int i = 1; i <= n; i++) {
        printf("%d ", t1);
        int nextTerm = t1 + t2;
        t1 = t2;
        t2 = nextTerm;
    }

    printf("\nFinished!\n");
    return 0;
}
""",
                ["factorial"] = """
int factorial(int n) {
    if (n <= 1) {
        return 1;
    }
    return n * factorial(n - 1);
}

int main() {
    int num = 6;
    int result = factorial(num);
    printf("Factorial of %d is: %d\n", num, result);
    return 0;
}
""",
                ["conditions"] = """
int main() {
    int score = 85;
    printf("Testing conditions for score: %d\n", score);

    if (score >= 90) {
        printf("Grade: A\n");
    } else if (score >= 80) {
        printf("Grade: B\n");
    } else {
        printf("Grade: C or below\n");
    }

    int count = 3;
    while (count > 0) {
        printf("Countdown: %d\n", count);
        count = count - 1;
    }

    printf("Blastoff!\n");
    return 0;
}
"""
            };

        static int Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Clear();
            Console.OutputEncoding = Encoding.UTF8;

            try
            {
                if (args.Length == 0)
                {
                    return InteractiveMode();
                }

                if (args.Length != 2)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  CToNasm.exe input.c output.asm");
                    return 1;
                }

                CompileFile(args[0], args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex.Message);
                Console.ResetColor();
                return 1;
            }
        }

        static int InteractiveMode()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("       C to NASM x86_64 Compiler");
            Console.WriteLine("             Linux Syscalls");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            Console.WriteLine("Available examples:");

            foreach (string name in Examples.Keys)
                Console.WriteLine("  " + name);

            Console.WriteLine();
            Console.Write("Example name or input C file: ");

            string input = Console.ReadLine() ?? "";

            if (Examples.TryGetValue(input, out string? example))
            {
                Console.Write("Output assembly file [program.asm]: ");
                string output = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(output))
                    output = "program.asm";

                CompileSource(example, output);
                return 0;
            }

            if (!File.Exists(input))
            {
                Console.WriteLine("Input file not found.");
                return 1;
            }

            Console.Write("Output assembly file [program.asm]: ");
            string outputFile = Console.ReadLine() ?? "";

            if (string.IsNullOrWhiteSpace(outputFile))
                outputFile = "program.asm";

            CompileFile(input, outputFile);
            return 0;
        }

        static void CompileFile(string inputFile, string outputFile)
        {
            if (!File.Exists(inputFile))
                throw new Exception("Input file not found: " + inputFile);

            string source = File.ReadAllText(inputFile);
            CompileSource(source, outputFile);
        }

        static void CompileSource(string source, string outputFile)
        {
            if (source.Contains("#include"))
                throw new Exception("Directive '#include' is strictly disallowed.");

            Lexer lexer = new Lexer(source);
            List<Token> tokens = lexer.Tokenize();

            Parser parser = new Parser(tokens);
            ProgramNode ast = parser.Parse();

            CodeGenerator generator = new CodeGenerator(ast);
            string assembly = generator.Generate();

            File.WriteAllText(outputFile, assembly, new UTF8Encoding(false));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("Compilation successful!");
            Console.ResetColor();

            Console.WriteLine("Input lines:  " + source.Split('\n').Length);
            Console.WriteLine("Output lines: " + assembly.Split('\n').Length);
            Console.WriteLine("Output file:  " + Path.GetFullPath(outputFile));
            Console.WriteLine();
            Console.WriteLine("Build command:");
            Console.WriteLine("nasm -f elf64 " + outputFile +
                              " -o out.o && ld out.o -o out && ./out");
        }
    }

    // ============================================================
    // TOKENS AND LEXER
    // ============================================================

    internal class Token
    {
        public string Type { get; }
        public string Value { get; }
        public int Line { get; }

        public Token(string type, string value, int line)
        {
            Type = type;
            Value = value;
            Line = line;
        }
    }

    internal class Lexer
    {
        private readonly string input;
        private int pos;
        private int line = 1;

        public Lexer(string input)
        {
            this.input = input;
        }

        private char? Peek(int offset = 0)
        {
            int index = pos + offset;

            if (index >= input.Length)
                return null;

            return input[index];
        }

        private char Next()
        {
            char ch = input[pos++];

            if (ch == '\n')
                line++;

            return ch;
        }

        private void SkipWhitespaceAndComments()
        {
            while (Peek() != null)
            {
                char ch = Peek()!.Value;

                if (char.IsWhiteSpace(ch))
                {
                    Next();
                }
                else if (ch == '/' && Peek(1) == '/')
                {
                    while (Peek() != null && Peek() != '\n')
                        Next();
                }
                else if (ch == '/' && Peek(1) == '*')
                {
                    Next();
                    Next();

                    while (Peek() != null &&
                           !(Peek() == '*' && Peek(1) == '/'))
                    {
                        Next();
                    }

                    if (Peek() == null)
                        throw new Exception("Line " + line +
                                            ": Unterminated block comment.");

                    Next();
                    Next();
                }
                else
                {
                    break;
                }
            }
        }

        private Token ReadString()
        {
            int startLine = line;
            Next();

            StringBuilder result = new StringBuilder();

            while (Peek() != null && Peek() != '"')
            {
                if (Peek() == '\\')
                {
                    Next();

                    if (Peek() == null)
                        break;

                    char escaped = Next();

                    switch (escaped)
                    {
                        case 'n': result.Append('\n'); break;
                        case 't': result.Append('\t'); break;
                        case 'r': result.Append('\r'); break;
                        case '\\': result.Append('\\'); break;
                        case '"': result.Append('"'); break;
                        case '0': result.Append('\0'); break;
                        default: result.Append(escaped); break;
                    }
                }
                else
                {
                    result.Append(Next());
                }
            }

            if (Peek() != '"')
                throw new Exception("Line " + startLine +
                                    ": Unterminated string.");

            Next();

            return new Token("STRING", result.ToString(), startLine);
        }

        private Token ReadNumber()
        {
            int startLine = line;
            StringBuilder number = new StringBuilder();

            while (Peek() != null && char.IsDigit(Peek()!.Value))
                number.Append(Next());

            return new Token("NUMBER", number.ToString(), startLine);
        }

        private Token ReadIdentifier()
        {
            int startLine = line;
            StringBuilder identifier = new StringBuilder();

            while (Peek() != null &&
                   (char.IsLetterOrDigit(Peek()!.Value) || Peek() == '_'))
            {
                identifier.Append(Next());
            }

            string value = identifier.ToString();

            string[] keywords =
            {
                "int", "void", "return", "if", "else",
                "while", "for", "printf"
            };

            if (keywords.Contains(value))
                return new Token(value.ToUpperInvariant(), value, startLine);

            return new Token("IDENT", value, startLine);
        }

        public List<Token> Tokenize()
        {
            List<Token> tokens = new List<Token>();

            SkipWhitespaceAndComments();

            while (pos < input.Length)
            {
                char ch = Peek()!.Value;
                int currentLine = line;

                if (ch == '"')
                {
                    tokens.Add(ReadString());
                }
                else if (char.IsDigit(ch))
                {
                    tokens.Add(ReadNumber());
                }
                else if (char.IsLetter(ch) || ch == '_')
                {
                    tokens.Add(ReadIdentifier());
                }
                else
                {
                    string two = ch.ToString() + (Peek(1)?.ToString() ?? "");

                    string[] doubleOperators =
                    {
                        "==", "!=", "<=", ">=", "&&", "||",
                        "++", "--", "+=", "-="
                    };

                    if (doubleOperators.Contains(two))
                    {
                        Next();
                        Next();
                        tokens.Add(new Token("OP", two, currentLine));
                    }
                    else if ("+-*/%=<>!(){};,".Contains(ch))
                    {
                        Next();
                        tokens.Add(new Token(ch.ToString(),
                                             ch.ToString(),
                                             currentLine));
                    }
                    else
                    {
                        throw new Exception(
                            "Line " + currentLine +
                            ": Unexpected character '" + ch + "'");
                    }
                }

                SkipWhitespaceAndComments();
            }

            tokens.Add(new Token("EOF", "EOF", line));

            return tokens;
        }
    }

    // ============================================================
    // AST NODES
    // ============================================================

    internal class ProgramNode
    {
        public List<FunctionNode> Functions = new List<FunctionNode>();
    }

    internal class FunctionNode
    {
        public string Name = "";
        public string ReturnType = "";
        public List<ParameterNode> Parameters = new List<ParameterNode>();
        public BlockNode Body = new BlockNode();
    }

    internal class ParameterNode
    {
        public string Name = "";
        public string Type = "";
    }

    internal abstract class Statement { }

    internal class BlockNode : Statement
    {
        public List<Statement> Statements = new List<Statement>();
    }

    internal class VariableDeclaration : Statement
    {
        public string Name = "";
        public Expression? Initializer;
    }

    internal class IfStatement : Statement
    {
        public Expression Condition = null!;
        public Statement ThenBranch = null!;
        public Statement? ElseBranch;
    }

    internal class WhileStatement : Statement
    {
        public Expression Condition = null!;
        public Statement Body = null!;
    }

    internal class ForStatement : Statement
    {
        public Statement? Initializer;
        public Expression? Condition;
        public Expression? Increment;
        public Statement Body = null!;
    }

    internal class ReturnStatement : Statement
    {
        public Expression? Value;
    }

    internal class ExpressionStatement : Statement
    {
        public Expression Value = null!;
    }

    internal abstract class Expression { }

    internal class LiteralExpression : Expression
    {
        public string Value = "";
    }

    internal class StringExpression : Expression
    {
        public string Value = "";
    }

    internal class VariableExpression : Expression
    {
        public string Name = "";
    }

    internal class UnaryExpression : Expression
    {
        public string Operator = "";
        public Expression Right = null!;
    }

    internal class PostfixExpression : Expression
    {
        public string Operator = "";
        public Expression Target = null!;
    }

    internal class BinaryExpression : Expression
    {
        public string Operator = "";
        public Expression Left = null!;
        public Expression Right = null!;
    }

    internal class AssignmentExpression : Expression
    {
        public string Name = "";
        public Expression Value = null!;
    }

    internal class CallExpression : Expression
    {
        public string Name = "";
        public List<Expression> Arguments = new List<Expression>();
    }

    internal class PrintfExpression : Expression
    {
        public List<Expression> Arguments = new List<Expression>();
    }

    // ============================================================
    // PARSER
    // ============================================================

    internal class Parser
    {
        private readonly List<Token> tokens;
        private int current;

        public Parser(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        private Token Peek() => tokens[current];

        private Token Previous() => tokens[current - 1];

        private bool IsAtEnd() => Peek().Type == "EOF";

        private bool Check(string type)
        {
            if (IsAtEnd())
                return false;

            return Peek().Type == type;
        }

        private bool Match(params string[] types)
        {
            foreach (string type in types)
            {
                if (Check(type))
                {
                    current++;
                    return true;
                }
            }

            return false;
        }

        private Token Consume(string type, string message)
        {
            if (Check(type))
                return tokens[current++];

            throw new Exception(
                "Line " + Peek().Line + ": " + message +
                ". Got '" + Peek().Value + "'");
        }

        private string ConsumeType()
        {
            if (Match("INT"))
                return "int";

            if (Match("VOID"))
                return "void";

            throw new Exception(
                "Line " + Peek().Line +
                ": Expected type 'int' or 'void'.");
        }

        public ProgramNode Parse()
        {
            ProgramNode program = new ProgramNode();

            while (!IsAtEnd())
                program.Functions.Add(ParseFunction());

            if (program.Functions.Count == 0)
                throw new Exception("No functions found.");

            if (!program.Functions.Any(f => f.Name == "main"))
                throw new Exception("Function 'main' was not found.");

            return program;
        }

        private FunctionNode ParseFunction()
        {
            string returnType = ConsumeType();

            string name = Consume("IDENT",
                "Expected function name").Value;

            Consume("(", "Expected '(' after function name");

            List<ParameterNode> parameters = new List<ParameterNode>();

            if (!Check(")"))
            {
                do
                {
                    string type = ConsumeType();
                    string parameterName = Consume("IDENT",
                        "Expected parameter name").Value;

                    parameters.Add(new ParameterNode
                    {
                        Type = type,
                        Name = parameterName
                    });
                }
                while (Match(","));
            }

            Consume(")", "Expected ')' after parameters");

            BlockNode body = ParseBlock();

            return new FunctionNode
            {
                Name = name,
                ReturnType = returnType,
                Parameters = parameters,
                Body = body
            };
        }

        private BlockNode ParseBlock()
        {
            Consume("{", "Expected '{' to start block");

            BlockNode block = new BlockNode();

            while (!Check("}") && !IsAtEnd())
                block.Statements.Add(ParseStatement());

            Consume("}", "Expected '}' after block");

            return block;
        }

        private Statement ParseStatement()
        {
            if (Check("INT"))
                return ParseVariableDeclaration();

            if (Match("IF"))
                return ParseIf();

            if (Match("WHILE"))
                return ParseWhile();

            if (Match("FOR"))
                return ParseFor();

            if (Match("RETURN"))
                return ParseReturn();

            if (Check("{"))
                return ParseBlock();

            return ParseExpressionStatement();
        }

        private VariableDeclaration ParseVariableDeclaration()
        {
            ConsumeType();

            string name = Consume("IDENT",
                "Expected variable name").Value;

            Expression? initializer = null;

            if (Match("="))
                initializer = ParseExpression();

            Consume(";", "Expected ';' after variable declaration");

            return new VariableDeclaration
            {
                Name = name,
                Initializer = initializer
            };
        }

        private Statement ParseIf()
        {
            Consume("(", "Expected '(' after if");

            Expression condition = ParseExpression();

            Consume(")", "Expected ')' after condition");

            Statement thenBranch = ParseStatement();

            Statement? elseBranch = null;

            if (Match("ELSE"))
                elseBranch = ParseStatement();

            return new IfStatement
            {
                Condition = condition,
                ThenBranch = thenBranch,
                ElseBranch = elseBranch
            };
        }

        private Statement ParseWhile()
        {
            Consume("(", "Expected '(' after while");

            Expression condition = ParseExpression();

            Consume(")", "Expected ')' after condition");

            return new WhileStatement
            {
                Condition = condition,
                Body = ParseStatement()
            };
        }

        private Statement ParseFor()
        {
            Consume("(", "Expected '(' after for");

            Statement? initializer = null;

            if (!Match(";"))
            {
                if (Check("INT"))
                    initializer = ParseVariableDeclaration();
                else
                    initializer = ParseExpressionStatement();
            }

            Expression? condition = null;

            if (!Check(";"))
                condition = ParseExpression();

            Consume(";", "Expected ';' after for condition");

            Expression? increment = null;

            if (!Check(")"))
                increment = ParseExpression();

            Consume(")", "Expected ')' after for clauses");

            return new ForStatement
            {
                Initializer = initializer,
                Condition = condition,
                Increment = increment,
                Body = ParseStatement()
            };
        }

        private Statement ParseReturn()
        {
            Expression? value = null;

            if (!Check(";"))
                value = ParseExpression();

            Consume(";", "Expected ';' after return");

            return new ReturnStatement { Value = value };
        }

        private Statement ParseExpressionStatement()
        {
            Expression expression = ParseExpression();

            Consume(";", "Expected ';' after expression");

            return new ExpressionStatement { Value = expression };
        }

        private Expression ParseExpression()
        {
            return ParseAssignment();
        }

        private Expression ParseAssignment()
        {
            Expression expression = ParseLogicalOr();

            if (Match("="))
            {
                Expression value = ParseAssignment();

                if (expression is VariableExpression variable)
                {
                    return new AssignmentExpression
                    {
                        Name = variable.Name,
                        Value = value
                    };
                }

                throw new Exception(
                    "Line " + Peek().Line +
                    ": Invalid assignment target.");
            }

            return expression;
        }

        private Expression ParseLogicalOr()
        {
            Expression expression = ParseLogicalAnd();

            while (Check("OP") && Peek().Value == "||")
            {
                string op = tokens[current++].Value;

                expression = new BinaryExpression
                {
                    Operator = op,
                    Left = expression,
                    Right = ParseLogicalAnd()
                };
            }

            return expression;
        }

        private Expression ParseLogicalAnd()
        {
            Expression expression = ParseEquality();

            while (Check("OP") && Peek().Value == "&&")
            {
                string op = tokens[current++].Value;

                expression = new BinaryExpression
                {
                    Operator = op,
                    Left = expression,
                    Right = ParseEquality()
                };
            }

            return expression;
        }

        private Expression ParseEquality()
        {
            Expression expression = ParseRelational();

            while (Check("OP") &&
                   (Peek().Value == "==" || Peek().Value == "!="))
            {
                string op = tokens[current++].Value;

                expression = new BinaryExpression
                {
                    Operator = op,
                    Left = expression,
                    Right = ParseRelational()
                };
            }

            return expression;
        }

        private Expression ParseRelational()
        {
            Expression expression = ParseAdditive();

            while ((Check("OP") &&
                    (Peek().Value == "<=" || Peek().Value == ">=")) ||
                   Check("<") || Check(">"))
            {
                string op = tokens[current++].Value;

                expression = new BinaryExpression
                {
                    Operator = op,
                    Left = expression,
                    Right = ParseAdditive()
                };
            }

            return expression;
        }

        private Expression ParseAdditive()
        {
            Expression expression = ParseMultiplicative();

            while (Match("+", "-"))
            {
                string op = Previous().Value;

                expression = new BinaryExpression
                {
                    Operator = op,
                    Left = expression,
                    Right = ParseMultiplicative()
                };
            }

            return expression;
        }

        private Expression ParseMultiplicative()
        {
            Expression expression = ParseUnary();

            while (Match("*", "/", "%"))
            {
                string op = Previous().Value;

                expression = new BinaryExpression
                {
                    Operator = op,
                    Left = expression,
                    Right = ParseUnary()
                };
            }

            return expression;
        }

        private Expression ParseUnary()
        {
            if (Match("-", "!"))
            {
                return new UnaryExpression
                {
                    Operator = Previous().Value,
                    Right = ParseUnary()
                };
            }

            return ParsePostfix();
        }

        private Expression ParsePostfix()
        {
            Expression expression = ParsePrimary();

            if (Check("OP") &&
                (Peek().Value == "++" || Peek().Value == "--"))
            {
                string op = tokens[current++].Value;

                return new PostfixExpression
                {
                    Operator = op,
                    Target = expression
                };
            }

            return expression;
        }

        private Expression ParsePrimary()
        {
            if (Match("NUMBER"))
                return new LiteralExpression { Value = Previous().Value };

            if (Match("STRING"))
                return new StringExpression { Value = Previous().Value };

            if (Match("PRINTF"))
                return ParsePrintf();

            if (Match("IDENT"))
            {
                string name = Previous().Value;

                if (Match("("))
                {
                    List<Expression> arguments = ParseArguments();

                    Consume(")", "Expected ')' after arguments");

                    return new CallExpression
                    {
                        Name = name,
                        Arguments = arguments
                    };
                }

                return new VariableExpression { Name = name };
            }

            if (Match("("))
            {
                Expression expression = ParseExpression();

                Consume(")", "Expected ')' after expression");

                return expression;
            }

            throw new Exception(
                "Line " + Peek().Line +
                ": Unexpected token '" + Peek().Value + "'");
        }

        private PrintfExpression ParsePrintf()
        {
            Consume("(", "Expected '(' after printf");

            List<Expression> arguments = ParseArguments();

            Consume(")", "Expected ')' after printf arguments");

            return new PrintfExpression { Arguments = arguments };
        }

        private List<Expression> ParseArguments()
        {
            List<Expression> arguments = new List<Expression>();

            if (!Check(")"))
            {
                do
                {
                    arguments.Add(ParseExpression());
                }
                while (Match(","));
            }

            return arguments;
        }
    }

    // ============================================================
    // NASM CODE GENERATOR
    // ============================================================

    internal class CodeGenerator
    {
        private readonly ProgramNode ast;

        private readonly List<(string Id, string Value)> strings =
            new List<(string Id, string Value)>();

        private int labelCounter;
        private Dictionary<string, int> locals =
            new Dictionary<string, int>();

        private int stackOffset;

        public CodeGenerator(ProgramNode ast)
        {
            this.ast = ast;
        }

        private string NewLabel(string prefix)
        {
            return "." + prefix + "_" + labelCounter++;
        }

        private string AddString(string value)
        {
            string id = "str_" + strings.Count;

            strings.Add((id, value));

            return id;
        }

        public string Generate()
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine("; ============================================================");
            text.AppendLine("; Generated by C to NASM x86_64 Compiler");
            text.AppendLine("; Linux System V ABI");
            text.AppendLine("; sys_write = 1, sys_exit = 60");
            text.AppendLine("; ============================================================");
            text.AppendLine();

            StringBuilder code = new StringBuilder();

            code.AppendLine("section .text");
            code.AppendLine("global _start");

            foreach (FunctionNode function in ast.Functions)
                code.AppendLine("global " + function.Name);

            code.AppendLine();
            code.AppendLine("_start:");
            code.AppendLine("    and rsp, -16");
            code.AppendLine("    call main");
            code.AppendLine("    mov rdi, rax");
            code.AppendLine("    mov rax, 60");
            code.AppendLine("    syscall");
            code.AppendLine();

            foreach (FunctionNode function in ast.Functions)
            {
                code.AppendLine(GenerateFunction(function));
            }

            code.AppendLine(GeneratePrintfRuntime());

            StringBuilder data = new StringBuilder();

            data.AppendLine("section .rodata");

            foreach (var item in strings)
            {
                data.Append("    ");
                data.Append(item.Id);
                data.Append(": db ");
                data.Append(FormatNasmBytes(item.Value));
                data.AppendLine(", 0");
            }

            data.AppendLine();

            return text.ToString() + data + code;
        }

        private string FormatNasmBytes(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            List<string> parts = new List<string>();
            StringBuilder printable = new StringBuilder();

            foreach (byte b in bytes)
            {
                if (b >= 32 && b <= 126 && b != 34 && b != 92)
                {
                    printable.Append((char)b);
                }
                else
                {
                    if (printable.Length > 0)
                    {
                        parts.Add("\"" + printable + "\"");
                        printable.Clear();
                    }

                    parts.Add(b.ToString());
                }
            }

            if (printable.Length > 0)
                parts.Add("\"" + printable + "\"");

            return parts.Count == 0 ? "0" : string.Join(", ", parts);
        }

        private string GenerateFunction(FunctionNode function)
        {
            locals = new Dictionary<string, int>();
            stackOffset = 0;

            ScanLocals(function.Body);

            foreach (ParameterNode parameter in function.Parameters)
            {
                if (locals.ContainsKey(parameter.Name))
                    throw new Exception("Duplicate variable '" +
                                        parameter.Name + "'.");

                stackOffset += 8;
                locals[parameter.Name] = -stackOffset;
            }

            int totalStack = stackOffset;

            if (totalStack % 16 != 0)
                totalStack += 16 - totalStack % 16;

            if (totalStack == 0)
                totalStack = 16;

            StringBuilder output = new StringBuilder();

            output.AppendLine(function.Name + ":");
            output.AppendLine("    push rbp");
            output.AppendLine("    mov rbp, rsp");
            output.AppendLine("    sub rsp, " + totalStack);

            string[] registers =
            {
                "rdi", "rsi", "rdx", "rcx", "r8", "r9"
            };

            if (function.Parameters.Count > 6)
                throw new Exception("Functions with more than 6 parameters are not supported.");

            for (int i = 0; i < function.Parameters.Count; i++)
            {
                int offset = locals[function.Parameters[i].Name];

                output.AppendLine("    mov [rbp " + offset + "], " +
                                  registers[i]);
            }

            output.Append(GenerateBlock(function.Body));

            output.AppendLine("    mov rax, 0");
            output.AppendLine("    mov rsp, rbp");
            output.AppendLine("    pop rbp");
            output.AppendLine("    ret");

            return output.ToString();
        }

        private void ScanLocals(Statement statement)
        {
            if (statement == null)
                return;

            if (statement is BlockNode block)
            {
                foreach (Statement child in block.Statements)
                    ScanLocals(child);
            }
            else if (statement is VariableDeclaration variable)
            {
                if (locals.ContainsKey(variable.Name))
                    throw new Exception("Duplicate variable '" +
                                        variable.Name + "'.");

                stackOffset += 8;
                locals[variable.Name] = -stackOffset;
            }
            else if (statement is ForStatement forStatement)
            {
                if (forStatement.Initializer != null)
                    ScanLocals(forStatement.Initializer);

                ScanLocals(forStatement.Body);
            }
            else if (statement is IfStatement ifStatement)
            {
                ScanLocals(ifStatement.ThenBranch);

                if (ifStatement.ElseBranch != null)
                    ScanLocals(ifStatement.ElseBranch);
            }
            else if (statement is WhileStatement whileStatement)
            {
                ScanLocals(whileStatement.Body);
            }
        }

        private string GenerateBlock(BlockNode block)
        {
            StringBuilder output = new StringBuilder();

            foreach (Statement statement in block.Statements)
                output.Append(GenerateStatement(statement));

            return output.ToString();
        }

        private string GenerateStatement(Statement statement)
        {
            StringBuilder output = new StringBuilder();

            if (statement is VariableDeclaration variable)
            {
                if (variable.Initializer != null)
                {
                    output.Append(GenerateExpression(variable.Initializer));

                    int offset = GetVariableOffset(variable.Name);

                    output.AppendLine("    mov [rbp " + offset + "], rax");
                }
            }
            else if (statement is ExpressionStatement expressionStatement)
            {
                output.Append(GenerateExpression(expressionStatement.Value));
            }
            else if (statement is ReturnStatement returnStatement)
            {
                if (returnStatement.Value != null)
                    output.Append(GenerateExpression(returnStatement.Value));
                else
                    output.AppendLine("    mov rax, 0");

                output.AppendLine("    mov rsp, rbp");
                output.AppendLine("    pop rbp");
                output.AppendLine("    ret");
            }
            else if (statement is BlockNode block)
            {
                output.Append(GenerateBlock(block));
            }
            else if (statement is IfStatement ifStatement)
            {
                string elseLabel = NewLabel("else");
                string endLabel = NewLabel("endif");

                output.Append(GenerateExpression(ifStatement.Condition));
                output.AppendLine("    cmp rax, 0");

                output.AppendLine("    je " +
                    (ifStatement.ElseBranch != null
                        ? elseLabel
                        : endLabel));

                output.Append(GenerateStatement(ifStatement.ThenBranch));

                if (ifStatement.ElseBranch != null)
                {
                    output.AppendLine("    jmp " + endLabel);
                    output.AppendLine(elseLabel + ":");
                    output.Append(GenerateStatement(ifStatement.ElseBranch));
                }

                output.AppendLine(endLabel + ":");
            }
            else if (statement is WhileStatement whileStatement)
            {
                string start = NewLabel("while_start");
                string end = NewLabel("while_end");

                output.AppendLine(start + ":");
                output.Append(GenerateExpression(whileStatement.Condition));
                output.AppendLine("    cmp rax, 0");
                output.AppendLine("    je " + end);

                output.Append(GenerateStatement(whileStatement.Body));

                output.AppendLine("    jmp " + start);
                output.AppendLine(end + ":");
            }
            else if (statement is ForStatement forStatement)
            {
                string start = NewLabel("for_start");
                string end = NewLabel("for_end");

                if (forStatement.Initializer != null)
                    output.Append(GenerateStatement(forStatement.Initializer));

                output.AppendLine(start + ":");

                if (forStatement.Condition != null)
                {
                    output.Append(GenerateExpression(forStatement.Condition));
                    output.AppendLine("    cmp rax, 0");
                    output.AppendLine("    je " + end);
                }

                output.Append(GenerateStatement(forStatement.Body));

                if (forStatement.Increment != null)
                    output.Append(GenerateExpression(forStatement.Increment));

                output.AppendLine("    jmp " + start);
                output.AppendLine(end + ":");
            }

            return output.ToString();
        }

        private int GetVariableOffset(string name)
        {
            if (!locals.TryGetValue(name, out int offset))
                throw new Exception("Undefined variable '" + name + "'.");

            return offset;
        }

        private string GenerateExpression(Expression expression)
        {
            StringBuilder output = new StringBuilder();

            if (expression is LiteralExpression literal)
            {
                output.AppendLine("    mov rax, " + literal.Value);
            }
            else if (expression is StringExpression str)
            {
                string id = AddString(str.Value);

                output.AppendLine("    lea rax, [rel " + id + "]");
            }
            else if (expression is VariableExpression variable)
            {
                int offset = GetVariableOffset(variable.Name);

                output.AppendLine("    mov rax, [rbp " + offset + "]");
            }
            else if (expression is UnaryExpression unary)
            {
                output.Append(GenerateExpression(unary.Right));

                if (unary.Operator == "-")
                {
                    output.AppendLine("    neg rax");
                }
                else if (unary.Operator == "!")
                {
                    output.AppendLine("    cmp rax, 0");
                    output.AppendLine("    sete al");
                    output.AppendLine("    movzx rax, al");
                }
            }
            else if (expression is PostfixExpression postfix)
            {
                if (postfix.Target is not VariableExpression target)
                    throw new Exception("Postfix operator requires a variable.");

                int offset = GetVariableOffset(target.Name);

                output.AppendLine("    mov rax, [rbp " + offset + "]");

                if (postfix.Operator == "++")
                    output.AppendLine("    add qword [rbp " + offset + "], 1");
                else
                    output.AppendLine("    sub qword [rbp " + offset + "], 1");
            }
            else if (expression is AssignmentExpression assignment)
            {
                output.Append(GenerateExpression(assignment.Value));

                int offset = GetVariableOffset(assignment.Name);

                output.AppendLine("    mov [rbp " + offset + "], rax");
            }
            else if (expression is BinaryExpression binary)
            {
                output.Append(GenerateExpression(binary.Left));
                output.AppendLine("    push rax");

                output.Append(GenerateExpression(binary.Right));

                output.AppendLine("    mov rbx, rax");
                output.AppendLine("    pop rax");

                switch (binary.Operator)
                {
                    case "+": output.AppendLine("    add rax, rbx"); break;
                    case "-": output.AppendLine("    sub rax, rbx"); break;
                    case "*": output.AppendLine("    imul rax, rbx"); break;

                    case "/":
                        output.AppendLine("    cqo");
                        output.AppendLine("    idiv rbx");
                        break;

                    case "%":
                        output.AppendLine("    cqo");
                        output.AppendLine("    idiv rbx");
                        output.AppendLine("    mov rax, rdx");
                        break;

                    case "==":
                        output.AppendLine("    cmp rax, rbx");
                        output.AppendLine("    sete al");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case "!=":
                        output.AppendLine("    cmp rax, rbx");
                        output.AppendLine("    setne al");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case "<":
                        output.AppendLine("    cmp rax, rbx");
                        output.AppendLine("    setl al");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case "<=":
                        output.AppendLine("    cmp rax, rbx");
                        output.AppendLine("    setle al");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case ">":
                        output.AppendLine("    cmp rax, rbx");
                        output.AppendLine("    setg al");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case ">=":
                        output.AppendLine("    cmp rax, rbx");
                        output.AppendLine("    setge al");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case "&&":
                        output.AppendLine("    test rax, rax");
                        output.AppendLine("    setne al");
                        output.AppendLine("    test rbx, rbx");
                        output.AppendLine("    setne bl");
                        output.AppendLine("    and al, bl");
                        output.AppendLine("    movzx rax, al");
                        break;

                    case "||":
                        output.AppendLine("    or rax, rbx");
                        output.AppendLine("    setne al");
                        output.AppendLine("    movzx rax, al");
                        break;
                }
            }
            else if (expression is CallExpression call)
            {
                output.Append(GenerateCall(call.Name, call.Arguments));
            }
            else if (expression is PrintfExpression printf)
            {
                if (printf.Arguments.Count == 0)
                    throw new Exception("printf requires a format string.");

                if (printf.Arguments.Count > 6)
                    throw new Exception("printf supports a maximum of 6 arguments.");

                output.Append(GenerateCall("__builtin_printf",
                                           printf.Arguments));
            }

            return output.ToString();
        }

        private string GenerateCall(string name, List<Expression> arguments)
        {
            if (arguments.Count > 6)
                throw new Exception("A maximum of 6 function arguments is supported.");

            StringBuilder output = new StringBuilder();

            string[] registers =
            {
                "rdi", "rsi", "rdx", "rcx", "r8", "r9"
            };

            for (int i = arguments.Count - 1; i >= 0; i--)
            {
                output.Append(GenerateExpression(arguments[i]));
                output.AppendLine("    push rax");
            }

            for (int i = 0; i < arguments.Count; i++)
                output.AppendLine("    pop " + registers[i]);

            // The generated function bodies preserve the stack alignment.
            output.AppendLine("    call " + name);

            return output.ToString();
        }

        // ========================================================
        // Minimal printf runtime using Linux sys_write
        // Supports %s, %d, %c and %%
        // ========================================================

        private string GeneratePrintfRuntime()
        {
            return """
; ============================================================
; Built-in printf runtime
; Supports: %s, %d, %c, %%
; Uses Linux sys_write directly
; ============================================================

__builtin_printf:
    push rbp
    mov rbp, rsp

    push rbx
    push r12
    push r13
    push r14
    push r15

    sub rsp, 64

    mov r12, rdi
    mov [rbp - 48], rsi
    mov [rbp - 56], rdx
    mov [rbp - 64], rcx
    mov [rbp - 72], r8
    mov [rbp - 80], r9

    lea r13, [rbp - 48]
    xor r14, r14

.printf_loop:
    mov al, [r12]
    test al, al
    jz .printf_done

    cmp al, '%'
    je .printf_format

    mov [rbp - 88], al
    mov rax, 1
    mov rdi, 1
    lea rsi, [rbp - 88]
    mov rdx, 1
    syscall

    inc r14
    inc r12
    jmp .printf_loop

.printf_format:
    inc r12
    mov al, [r12]
    test al, al
    jz .printf_done

    inc r12

    cmp al, '%'
    je .printf_percent

    cmp al, 'd'
    je .printf_integer

    cmp al, 's'
    je .printf_string

    cmp al, 'c'
    je .printf_character

    jmp .printf_loop

.printf_percent:
    mov byte [rbp - 88], '%'
    mov rsi, rbp
    sub rsi, 88
    mov rdx, 1
    jmp .printf_write_one

.printf_character:
    mov rax, [r13]
    add r13, 8
    mov [rbp - 88], al
    mov rsi, rbp
    sub rsi, 88
    mov rdx, 1
    jmp .printf_write_one

.printf_write_one:
    mov rax, 1
    mov rdi, 1
    syscall
    inc r14
    jmp .printf_loop

.printf_string:
    mov rsi, [r13]
    add r13, 8

    test rsi, rsi
    jnz .printf_string_length

    lea rsi, [rel .printf_null]

.printf_string_length:
    mov rdi, rsi
    xor rdx, rdx

.printf_string_scan:
    cmp byte [rdi + rdx], 0
    je .printf_string_write
    inc rdx
    jmp .printf_string_scan

.printf_string_write:
    test rdx, rdx
    jz .printf_loop

    mov rax, 1
    mov rdi, 1
    syscall

    add r14, rdx
    jmp .printf_loop

.printf_integer:
    mov rax, [r13]
    add r13, 8

    lea rsi, [rbp - 120]
    add rsi, 32

    xor rbx, rbx

    test rax, rax
    jns .printf_int_positive

    neg rax
    mov bl, 1

.printf_int_positive:
    mov rcx, 10

.printf_int_convert:
    xor rdx, rdx
    div rcx
    add dl, '0'
    dec rsi
    mov [rsi], dl
    test rax, rax
    jnz .printf_int_convert

    test bl, bl
    jz .printf_int_output

    dec rsi
    mov byte [rsi], '-'

.printf_int_output:
    lea rdx, [rbp - 88]
    add rdx, 1
    sub rdx, rsi

    mov rax, 1
    mov rdi, 1
    syscall

    add r14, rdx
    jmp .printf_loop

.printf_done:
    mov rax, r14

    add rsp, 64

    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx

    mov rsp, rbp
    pop rbp
    ret

.printf_null:
    db "(null)", 0
""";
        }
    }
}