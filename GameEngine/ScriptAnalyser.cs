using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace GameEngine
{
    /*
     * 
     * TABLE NEED:
     * 
     * t : double , 脚本运行时间
     * gt : double , 游戏运行总时间
     * prototype : List<Script> , 脚本原型（游戏对象初始化时会从这里复制脚本独自使用）
     * loadlist : List<Script> , 加载列表（脚本运行到load指令时会将创建对象加入到loadlist中，
     *                                      当同一时刻的脚本全部运行结束后，loadlist中的对象会同时创建）
     * 
     * script_block:generate(symboltable) 的返回值: 
     * 
     * -1 : 脚本存在自销毁请求
     * -2 : 脚本存在创建新对象的请求
     * -3 : 脚本请求为绑定游戏对象加载图片
     * 
     * 
     * Statements: Statements '\n'
     *          |  Statement
     *          
     * 
     * Statement: Exp ';'                           主要用于给对象的速度、位置等属性赋值 
     *          | 'destroy' ';'
     *          | 'image' str(image file path) ';'
     *          | 'debug' str(MSG) ';'              游戏运行过程中，该指令将会在日志窗口输出MSG
     *          | if_block ';'
     *          | while_block ';'
     *          | load_block ';'
     * 
     * Load_Block: 'load' str(Script name) Load_Optional_Part ';'   运行该指令将会创建一个游戏对象并根据脚本名绑定脚本，将该对象加入到loadlist中
     * 
     * Load_Optional_Part: ':' expr {',' expr}                      运行脚本的附加参数，expr从左往右依次赋值给创建脚本的参数arg0, arg1 ...
     *          
     * 
     * Script : '#' str(script name) '\n' script_block
     * 
     * Script_Block: Script_Block '\n' Script_Block
     *             | Time_Condition '\n' Statements
     *             | Keyboard_Event '\n' Statements
     * 
     * Time_Condition: 'at' double(start time) tc_optional_part ';'
     * 
     * TC_Optional_Part: ':' double (end time) TC_Optional_Part2
     *                 | TC_Optional_Part2
     *                 
     * TC_Optional_Part2: ',' double (interval time)
     *                  | 3
     * Time_Condition:
     * 
     * 时间触发条件分以下几类：
     * 1、只在固定时间点触发：at 1 代表在脚本运行1s之后触发且触发一次
     * 2、在固定时间段内连续触发：at 1:2 代表脚本在游戏时间[1,2]内触发次数等于游戏帧数
     * 3、有时间间隔的触发： at 1,0.2 代表从第1s开始，脚本每隔0.2s触发一次
     * 4、有时间间隔和触发时间段的触发： at 1:2,0.2 代表脚本在游戏时间[1,2]内每隔0.2s触发一次
     * 
     * Keyboard_Condition: 'at' 'key_up' ';'
     *                   | 'at' 'key_down' ';'
     *                   | 'at' 'key_left' ';'
     *                   | 'at' 'key_right' ';'
     *                   | 'at' 'key_slow' ';'  （SHIFT键：STG游戏中的角色可以低速移动）
     *                   | 'at' 'key_attack' ';' (Z键：STG游戏中的角色射击键)
     * 
     * If_Block: 'if' expr 'then' statements If_Optional_part 'iend'
     * 
     * If_Optional_part: 'else' statements
     * 
     * While_Block: 'while" expr 'then' statements
     * 
     * Exp: Exp_Two
     *    | Exp_One
     *    | Exp_Value
     * 
     * Exp_Value: number
     *          | id
     *          | (Exp)
     *    
     * Exp_Two: Exp_Value DOPE Exp_Value            对于逻辑运算表达式，真返回1，假返回0
     * 
     * DOPE: +, -, *, /, <, >, <=, >=, ==, !=
     * 
     * Exp_One: SOPE Exp_Value   
     * 
     * SOPE: sin, cos, tan, asin, acos, atan, abs, sqrt, exp, log， -
     * 
     * */



    class ScriptAnalyser
    {
        public string text = "";
        public char[] code;
        public int index, lineNo;

        public const string math_symbol =  "!<>+-*/()%=";
        public string[] keywords = new string[] { "destroy", "then", "while", "wend", 
            "iend", "at", "main", "if" , "else", "load", "image"};

        public List<Script> scan(string text)
        {
            this.text = text;
            code = text.ToArray();
            index = lineNo = 0;
            try
            {
                Script script = match_script();
                List<Script> parse = new List<Script>();
                while (script != null)
                {
                    parse.Add(script);
                    script = match_script();
                }
                LogWriter.WriteLogText("Analyser: Done.");
                return parse;
            }
            catch (Exception)
            {
                LogWriter.WriteLexText(lineNo, "Analyser process terminated");
                return new List<Script>();
            }
            
        }

        private Script match_script()
        {
            Script script = new Script();
            script.title = match_title();
            if (script.title == null) return null;
            script.script_block = new List<Script_Block>();
            Script_Block sb = match_script_block();
            while (sb != null)
            {
                script.script_block.Add(sb);
                sb = match_script_block();
            }
            return script;
        }

        private Script_Block match_script_block()
        {
            Script_Block script_block = new Script_Block();
            script_block.logic_Stat = match_logic_stat();
            if (script_block.logic_Stat == null)
                return null;
            script_block.expList = match_expressions();
            return script_block;
        }

        private void match_blank()
        {
            while (index < code.Length)
            {
                if (code[index] == ' ')
                    index++;
                else if (code[index] == '\t' || code[index] == '\r')
                    index++;
                else if (code[index] == '\n')
                {
                    lineNo++;
                    index++;
                }
                else break;
            }
        }

        private string match_title()
        {
            match_blank();
            if (!match("#")) return null;
            return match_string();
        }

        private string match_string()
        {
            match_blank();
            if (index >= code.Length) return null;
            string str = "";
            int lastIndex = index;
            while (index < code.Length)
            {
                if ('a' <= code[index] && code[index] <= 'z')
                    str += code[index++];
                else if ('A' <= code[index] && code[index] <= 'Z')
                    str += code[index++];
                else if (code[index] == '_')
                    str += code[index++];
                else if (index != lastIndex && '0' <= code[index] && code[index] <= '9')
                    str += code[index++];
                else break;
            }
            if(lastIndex != index)
                return str;
            return null;
        }



        private Logic_Stat match_logic_stat()
        {
            int backup = index;
            Logic_Stat logic_Stat = new Logic_Stat();
            if (!match("at")) return null;
            if (match("init"))
            {
                logic_Stat.startExp = Exp.getInstance(-1);
            }
            else if (match("final"))
            {
                logic_Stat.startExp = Exp.getInstance(-2);
            }
            else
            {
                logic_Stat.startExp = match_expression();
                if (logic_Stat == null) return null;

                if (match(":"))
                {
                    logic_Stat.endExp = match_expression();
                }
                if (match(","))
                {
                    logic_Stat.periodExp = match_expression();
                }
            }
            if (!match(";"))
            {
                LogWriter.WriteLexText(lineNo, "expect \";\"");
            }
            return (logic_Stat);
        }

        private Exp_Block match_expressions()
        {
            if (index == code.Length) return null;
            IGenerate exp = match_stat();
            List<IGenerate> expList = new List<IGenerate>();
            while (exp != null)
            {
                expList.Add(exp);
                exp = match_stat();
            }
            return new Exp_Block(expList);
        }

        private IGenerate check(IGenerate exp)
        {
            if (exp == null) return null;
            if (!match(";"))
            {
                LogWriter.WriteLexText(lineNo, "expect \";\"");
                return null;
            }
            return exp;
        }

        private string match_number()
        {
            int lastIndex = index;
            string str = "";
            while (index < code.Length)
            {
                if ('0' <= code[index] && code[index] <= '9')
                    str += code[index++];
                else if (code[index] == '.')
                    str += code[index++];
                else break;
            }
            
            if (lastIndex != index)
                return str;
            return null;
        }

        private IGenerate match_debug()
        {
            Debug_Stat ds = new Debug_Stat();
            ds.exp = match_expression();
            return check(ds);
        }

        private IGenerate match_while()
        {
            While_Stat ws = new While_Stat();
            ws.expression = match_expression();
            if (!match("then"))
            {
                LogWriter.WriteLexText(lineNo, "expect then");
                return null;
            }
            ws.expList = match_expressions();
            if (!match("wend"))
            {
                LogWriter.WriteLexText(lineNo, "expect wend");
                return null;
            }
            return check(ws);
        }

        private IGenerate match_if()
        {
            If_Stat if_Stat = new If_Stat();
            if_Stat.expression = match_expression();
            if (if_Stat == null)
            {
                LogWriter.WriteLexText(lineNo, "Expect expression");
                return null;
            }
            if (!match("then"))
            {
                LogWriter.WriteLexText(lineNo, "Expect then");
                return null;
            }
            if_Stat.expThen = match_expressions();
            if (match("else"))
                if_Stat.expElse = match_expressions();
            if (!match("iend"))
            {
                LogWriter.WriteLexText(lineNo, "Expect iend");
                return null;
            }
            return check(if_Stat);
        }

        private IGenerate match_load()
        {
            Load_Stat load_Stat = new Load_Stat();
            load_Stat.scriptName = match_string();
            load_Stat.pars = new List<Exp>();
            if (load_Stat.scriptName == null)
            {
                LogWriter.WriteLexText(lineNo, "expect script name");
                return null;
            }
            if (match(":"))
            {
                Exp exp = match_expression();
                while (match(","))
                {
                    load_Stat.pars.Add(exp);
                    exp = match_expression();
                }
                if (exp == null)
                {
                    LogWriter.WriteLexText(lineNo, "expect expression");
                    return null;
                }
                load_Stat.pars.Add(exp);
            }
            return check(load_Stat);
        }
        private IGenerate match_image()
        {
            string filename = match_filename();
            Image_Stat image_Stat = new Image_Stat();
            image_Stat.filename = filename;
            if (filename == null)
            {
                LogWriter.WriteLexText(lineNo, "expect filename");
                return null;
            }
            return check(image_Stat);
        }
        private IGenerate match_function_expression()
        {
            if (match("debug"))
                return match_debug();
            if (match("destroy"))
                return check(new Destroy_Stat());
            if (match("while"))
                return match_while();
            if (match("if"))
                return match_if();
            if (match("load"))
                return match_load();
            if (match("image"))
                return match_image();
            return null;
        }

        private bool isAlphabeta()
        {
            if ('a' <= code[index] && code[index] <= 'z')
                return true;
            if ('A' <= code[index] && code[index] <= 'Z')
                return true;
            return false;
        }

        private bool isNumber()
        {
            return '0' <= code[index] && code[index] <= '9';
        }

        private string match_filename()
        {
            match_blank();
            string str = "";
            int lastIndex = index;
            while (index < code.Length)
            {
                if (isAlphabeta() || isNumber())
                    str += code[index++];
                else if (code[index] == '\\' || code[index] == '/' || code[index] == '_' 
                    || code[index] == ' ' || code[index] ==':' || code[index] == '.')
                    str += code[index++];
                else break;
            }
            if (lastIndex == index)
                return null;
            return str;
        }

        private IGenerate match_stat()
        {
            if (match("at"))
            {
                index -= 2;
                return null;
            }
            else if (match("#"))
            {
                index--;
                return null;
            }
            
            IGenerate exp = match_function_expression();
            if (exp != null) return exp;
            exp = match_expression();
            return check(exp);
        }

        public bool wrongMatch(string token)
        {
            foreach (string keyword in keywords)
                if (token.Equals(keyword))
                    return true;
            return false;
        }

        public Exp match_expression()
        {
            if (index >= code.Length) return null;
            string str = "";
            string temp = "";

            while (index < code.Length)
            {
                match_blank();
                if ((temp = match_number()) != null)
                    str += temp;
                else if ((temp = match_string()) != null)
                {
                    if (wrongMatch(temp))
                    {
                        index -= temp.Length;
                        break;
                    }
                    str += temp;
                }
                    
                else if (math_symbol.Contains(code[index]))
                    str += code[index++];
                else
                    break;
            }
            if (str.Length == 0) return null;
            return Exp.CreateExpression(lineNo, str);
        }

        public bool match(String text)
        {
            match_blank();
            int backup = index;
            char[] t = text.ToArray();
            for(int i = 0; i < t.Length; i ++)
                if (!match(t[i]))
                {
                    index = backup;
                    return false;
                }
            return true;
        }

        public bool match(char c)
        {
            if (index >= code.Length) return false;
            if (code[index] != c)
                return false;
            index++;
            return true;
        }
    }

    class LogWriter
    {

        public static string logText = "";

        public static void WriteErrText(String text)
        {
            string txt = "[ERR]" + text;
            logText += txt + "\r\n";
            Console.WriteLine(txt);
        }

        public static void WriteLogText(String text)
        {
            string txt = "[LOG]" + text;
            logText += txt + "\r\n";
            Console.WriteLine(txt);
        }

        public static void WriteLexText(int lineNo, String text)
        {
            string txt = "Line : " + lineNo + " " + text;
            logText += txt + "\r\n";
            Console.WriteLine(txt);
        }
    }

    class Exp_Block : IGenerate
    {
        public List<IGenerate> statments;
        public double generate(SymbolTable table)
        {
            
            foreach (IGenerate stat in statments)
            {
                stat.generate(table);
                if (table.get("destroy") == 1)
                    return -1;
            }
            return 1;
        }
        public Exp_Block(List<IGenerate> expList)
        {
            this.statments = expList;
        }
    }

    class If_Stat : IGenerate
    {
        public Exp expression = null;
        public Exp_Block expThen = null;
        public Exp_Block expElse = null;

        public double generate(SymbolTable table)
        {
            if (expression.generate(table) != 0)
                return expThen.generate(table);
            if (expElse != null)
                return expElse.generate(table);
            return 0;
        }
    }

    class While_Stat : IGenerate
    {
        public Exp expression;
        public Exp_Block expList;

        public double generate(SymbolTable table)
        {
            while (expression.generate(table) != 0)
            {
                if (expList.generate(table) == -1)
                    return -1;
            }
            return 1;
        }
    }

    abstract class Exp : IGenerate
    {
        public abstract double generate(SymbolTable table);
        public abstract string getID();
        public abstract bool canSimplify();

        public static int simplifiedCount = 0;

        private static Exp simplify(Exp exp)
        {
            if (exp != null && exp.canSimplify() &&!(exp is Exp_Value))
            {
                simplifiedCount++;
                return getInstance(exp.generate(null));
            }
            return exp;
        }

        public static Exp getInstance(Exp left, Exp right, Exp_Operator ope)
        {
            Exp_Two ret = new Exp_Two();
            if (ope != Exp_Operator.EQUAL && ope != Exp_Operator.MINU && ope != Exp_Operator.DIVI)
            {
                ret.left = simplify(left);
                ret.right = simplify(right);
            }
            else
            {
                ret.left = left;
                ret.right = right;
            }
            ret.ope = ope;
            return simplify(ret);
        }

        public static Exp getInstance(Exp left, Exp right, char c1, char c2)
        {
            Exp_Operator ope = Exp_Operator.NONE;
            switch (c1)
            {
                case '<':
                    if (c2 == '=')
                        ope = Exp_Operator.LESS_EQUAL_THAN;
                    else
                        ope = Exp_Operator.LESS_THAN;
                    break;
                case '>':
                    if (c2 == '=')
                        ope = Exp_Operator.GREATER_EQUAL_THAN;
                    else
                        ope = Exp_Operator.GREATER_THAN;
                    break;
                case '=':
                    if (c2 == '=')
                        ope = Exp_Operator.IF_EQUAL;
                    else
                        LogWriter.WriteLogText("Lex Error: expect if equal");
                    break;
                case '!':
                    if (c2 == '=')
                        ope = Exp_Operator.NOT_EQUAL;
                    else
                        LogWriter.WriteLogText("Lex Error: expect if not equal");
                    break;
            }
            return getInstance(left, right, ope);
        }

        public static Exp getInstance(Exp left, Exp right, char c)
        {
            Exp_Operator ope = Exp_Operator.PLUS ;
            switch (c)
            {
                case '+': ope = Exp_Operator.PLUS;
                    break;
                case '-': ope = Exp_Operator.MINU;
                    break;
                case '*': ope = Exp_Operator.MULT;
                    break;
                case '/': ope = Exp_Operator.DIVI;
                    break;
                case '<': ope = Exp_Operator.LESS_THAN;
                    break;
                case '>': ope = Exp_Operator.GREATER_THAN;
                    break;
                default:
                    LogWriter.WriteErrText("Unknown Two operator:" + c);
                    break;
            }
            return getInstance(left, right, ope);
        }

        public static Exp getInstance(Exp expression, Exp_Operator ope)
        {
            Exp_One ret = new Exp_One();
            ret.expression = expression;
            ret.ope = ope;
                
            return simplify(ret);
        }

        public static Exp getInstance(string id)
        {
            Exp_ID ret = new Exp_ID();
            ret.id = id;
            return ret;
        }

        public static Exp getInstance(double value)
        {
            Exp_Value ret = new Exp_Value();
            ret.value = value;
            return ret;
        }

        public static Exp buildExp(int lineNo, char[] code, int left, int right)
        {
            int bracket_count = 0;
            int index0 = -1,index1 = -1, index2 = -1;
            for (int i = right - 1; i > left; i--)
            {
                
                if (code[i] == '(') 
                    bracket_count++;
                else if (code[i] == ')') 
                    bracket_count--;
                else if(bracket_count == 0)
                {
                    switch (code[i])
                    {
                        case '+': case '-':
                            if (index1 == -1)
                                index1 = i;
                            break;
                        case '*': case '/':
                            if (index2 == -1)
                                index2 = i;
                            break;
                        case '=':
                            if (i - 1 > left && (code[i - 1] == '<' || code[i - 1] == '>' || code[i - 1] == '=' ||
                                code[i - 1] == '!'))
                            {
                                if (index0 == -1)
                                    index0 = i - 1;
                                i--;
                            }
                            else
                                return getInstance(buildExp(lineNo, code, left, i), buildExp(lineNo, code, i + 1, right), 
                                    Exp_Operator.EQUAL);
                            break;
                        case '<':
                            if(index0 == -1)
                                index0 = i;
                            break;
                        case '>':
                            if(index0 == -1)
                                index0 = i;
                            break;
                    }
                }
            }
            if (index0 != -1)
            {
                if (index0 + 1 < right)
                {
                    return getInstance(buildExp(lineNo, code, left, index0),
                        buildExp(lineNo, code, index0 + (code[index0 + 1] == '=' ? 2 : 1), right)
                        , code[index0], code[index0 + 1]);
                }
                return getInstance(buildExp(lineNo, code, left, index0), buildExp(lineNo, code, index0 + 1, right), code[index0]);
                
            }
            if (index1 != -1)
                return getInstance(buildExp(lineNo, code, left, index1), buildExp(lineNo, code, index1 + 1, right), code[index1]);
            if (index2 != -1)
                return getInstance(buildExp(lineNo, code, left, index2), buildExp(lineNo, code, index2 + 1, right), code[index2]);
            if (code[left] == '-')
            {
                Exp exp = buildExp(lineNo, code, left + 1, right);
                if (exp.canSimplify())
                    return getInstance(-1 * exp.generate(null));
                return getInstance(exp, Exp_Operator.MINU);
            }
                
            
            string str = new string(code, left, right - left);
            if (str.StartsWith("sin("))
                return getInstance(buildExp(lineNo, code, left + 4, right - 1), Exp_Operator.SIN);
            else if (str.StartsWith("cos("))
                return getInstance(buildExp(lineNo, code, left + 4, right - 1), Exp_Operator.COS);
            else if (str.StartsWith("tan("))
                return getInstance(buildExp(lineNo, code, left + 4, right - 1), Exp_Operator.TAN);
            else if (str.StartsWith("abs("))
                return getInstance(buildExp(lineNo, code, left + 4, right - 1), Exp_Operator.ABS);
            else if (str.StartsWith("exp("))
                return getInstance(buildExp(lineNo, code, left + 4, right - 1), Exp_Operator.EXP);
            else if (str.StartsWith("log("))
                return getInstance(buildExp(lineNo, code, left + 4, right - 1), Exp_Operator.LOG);
            else if (str.StartsWith("sqrt("))
                return getInstance(buildExp(lineNo, code, left + 5, right - 1), Exp_Operator.SQRT);
            else if (str.StartsWith("asin("))
                return getInstance(buildExp(lineNo, code, left + 5, right - 1), Exp_Operator.ASIN);
            else if (str.StartsWith("acos("))
                return getInstance(buildExp(lineNo, code, left + 5, right - 1), Exp_Operator.ACOS);
            else if (str.StartsWith("atan("))
                return getInstance(buildExp(lineNo, code, left + 5, right - 1), Exp_Operator.ATAN);
            if (code[left] == '(')
                return getInstance(buildExp(lineNo, code, left + 1, right - 1), Exp_Operator.NONE);
            double d;

            if ('0' <= code[left] && code[left] <= '9')
            {
                if (Double.TryParse(str, out d))
                    return getInstance(d);
                else LogWriter.WriteLexText(lineNo, "UNSOLVED EXPRESSION:" + str);
            }
                
            return getInstance(str);
        }


        public static Exp CreateExpression(int lineNo, String str)
        {
            simplifiedCount = 0;
            Exp exp = simplify(buildExp(lineNo, str.ToCharArray(), 0, str.Length));
            if (simplifiedCount > 0)
            {
                LogWriter.WriteLexText(lineNo, "Expression has been simplified");
            }
            return exp;
        }
    }

    class Exp_One : Exp
    {
        public Exp expression;
        public Exp_Operator ope;
        public const double scale = 180.0 / Math.PI;
        public override double generate(SymbolTable table)
        {
            double d = expression.generate(table);
            
            switch (ope)
            {
                case Exp_Operator.ABS:
                    return Math.Abs(d);
                case Exp_Operator.ACOS:
                    return scale * Math.Acos(d);
                case Exp_Operator.ASIN:
                    return scale * Math.Asin(d);
                case Exp_Operator.ATAN:
                    return scale * Math.Atan(d);
                case Exp_Operator.COS:
                    return Math.Cos(d / scale);
                case Exp_Operator.EXP:
                    return Math.Exp(d);
                case Exp_Operator.LOG:
                    return Math.Log(d);
                case Exp_Operator.SIN:
                    return Math.Sin(d / scale);
                case Exp_Operator.SQRT:
                    return Math.Sqrt(d);
                case Exp_Operator.TAN:
                    return Math.Tan(d / scale);
                case Exp_Operator.MINU:
                    return -d;
                case Exp_Operator.NONE:
                    return d;
                default:
                    LogWriter.WriteErrText("Unkown single operator " + ope.ToString());
                    break;
            }
            return 0;
        }
        public override string getID()
        {
            return "ERR";
        }
        public override bool canSimplify()
        {
            return expression.canSimplify();
        }
    }

    class Exp_Two : Exp
    {
        public Exp left, right;
        public Exp_Operator ope;
        public override double generate(SymbolTable table)
        {
            double l = left.generate(table);
            double r = right.generate(table);
            switch (ope)
            {
                case Exp_Operator.DIVI:
                    if (r == 0) return 0;
                    return l / r;
                case Exp_Operator.MINU:
                    return l - r;
                case Exp_Operator.MULT:
                    return l * r;
                case Exp_Operator.PLUS:
                    return l + r;
                case Exp_Operator.EQUAL:
                    table.set(left.getID(), r);
                    return r;
                case Exp_Operator.LESS_THAN:
                    return l < r ? 1 : 0;
                case Exp_Operator.GREATER_THAN:
                    return l > r ? 1 : 0;
                case Exp_Operator.IF_EQUAL:
                    return l == r ? 1 : 0;
                case Exp_Operator.NOT_EQUAL:
                    return l != r ? 1 : 0;
                case Exp_Operator.LESS_EQUAL_THAN:
                    return l <= r ? 1 : 0;
                case Exp_Operator.GREATER_EQUAL_THAN:
                    return l >= r ? 1 : 0;
                default:
                    LogWriter.WriteErrText("Unknown double operator " + ope.ToString());
                    break;
            }
            return 0;
        }
        public override string getID()
        {
            return "ERR";
        }
        public override bool canSimplify()
        {
            return left.canSimplify() && right.canSimplify();
        }
    }

    

    class Exp_Value : Exp
    {
        public double value;
        public override double generate(SymbolTable table)
        {
            return value;
        }
        public override string getID()
        {
            return "ERR";
        }
        public override bool canSimplify()
        {
            return true;
        }
    }

    class Exp_ID : Exp
    {
        public string id = "";
        public override double generate(SymbolTable table)
        {
            if (table == null)
            {
                LogWriter.WriteErrText("symboltable is null");
                return 0;
            }
            return table.get(id);
        }
        public override string getID()
        {
            return id;
        }
        public override bool canSimplify()
        {
            return false;
        }
    }

    enum Exp_Operator
    {
        PLUS, MINU, MULT, DIVI, ABS, TAN, SIN, COS, SQRT, ASIN, ACOS, ATAN, EXP, LOG, EQUAL, NONE
        , LESS_THAN, GREATER_THAN, LESS_EQUAL_THAN, GREATER_EQUAL_THAN, IF_EQUAL, NOT_EQUAL
    }

    class Destroy_Stat : IGenerate
    {
        public double generate(SymbolTable table)
        {
            table.set("destroy", 1);
            return 0;
        }
    }

    class Image_Stat : IGenerate
    {
        public string filename;
        public double generate(SymbolTable table)
        {
            table.setImageFilename(filename);
            return 0;
        }
    }

    class Debug_Stat : IGenerate
    {
        public Exp exp;
        public double generate(SymbolTable table)
        {
            LogWriter.WriteLogText("value = " + exp.generate(table));
            return 0;
        }
    }

    class Load_Stat : IGenerate
    {
        public string scriptName;
        public List<Exp> pars;
        public double generate(SymbolTable table)
        {
            List<Script> prototype = table.getPrototype();
            List<Script> loadList = table.getLoadList();
            Script scriptX = null;
            foreach(Script script in prototype){
                if(script.title.Equals(scriptName)){
                    scriptX = script.clone();
                    break;
                }
            }
            if (scriptX == null)
            {
                LogWriter.WriteErrText("Script Name : " + scriptName + " Unfound");
                return 0;
            }
            for (int i = 0; i < pars.Count; i++)
                scriptX.preExp.Add(
                    Exp.getInstance(Exp.getInstance("arg" + i), Exp.getInstance(pars[i].generate(table)), Exp_Operator.EQUAL));

            loadList.Add(scriptX);
            return 0;
        }
    }

    class Logic_Stat : IGenerate
    {
        public Exp startExp;
        public Exp endExp;
        public Exp periodExp;
        private double lastPeriod = -1;

        public double generate(SymbolTable table)
        {
            double t = -1, st, et, pt;
            if (table == null)
            {
                t = 0;
                LogWriter.WriteErrText("SYMBOL TABLE NULL");
            }
            t = table.get("t");
            st = startExp.generate(table);
            if (endExp != null)
                et = endExp.generate(table);
            else et = -1;
            if (periodExp != null)
                pt = periodExp.generate(table);
            else pt = -1;
            if (st == -1)
            {
                if(t == 0)
                    return 1;
                return 0;
            }
            else if (st == -2)
            {
                return table.get("destroy") == 1 ? 1 : 0;
            }
            else if (st <= t && (et == -1 ||t <= et))
            {
                if (pt == -1 || lastPeriod == -1 ||t - lastPeriod >= pt)
                {
                    lastPeriod = t;
                    return 1;
                }
            }
            return 0;
        }

        public Logic_Stat clone()
        {
            Logic_Stat logic_Stat = new Logic_Stat();
            logic_Stat.startExp = startExp;
            logic_Stat.endExp = endExp;
            logic_Stat.periodExp = periodExp;
            return logic_Stat;
        }
    }

    class Script_Block : IGenerate
    {
        public Logic_Stat logic_Stat = new Logic_Stat();
        public Exp_Block expList = null;

        public double generate(SymbolTable table)
        {
            
            if (logic_Stat.generate(table) == 0)
                return 0;
            return expList.generate(table);
        }

        public Script_Block clone()
        {
            Script_Block sb = new Script_Block();
            sb.logic_Stat = logic_Stat.clone();
            sb.expList = expList;
            return sb;
        }
    }

    class Script : IGenerate
    {
        public string title = "";
        public List<Script_Block> script_block;
        public List<Exp> preExp = new List<Exp>();
        public double startTime = -1;
        public Script clone()
        {
            Script script = new Script();
            script.title = title;
            script.script_block = new List<Script_Block>();
            foreach (Script_Block sb in script_block)
                script.script_block.Add(sb.clone());
            return script;
        }

        public double generate(SymbolTable table)
        {
            table.ClearUnnecessary();
            double st = table.get("gt");
            if (startTime == -1)
            {
                startTime = st;
                st = 0;
            }
            else st -= startTime;
            table.set("t", st);

            int ret = 1;

            foreach (Exp exp in preExp)
                exp.generate(table);
            foreach (Script_Block sb in script_block)
                if (sb.generate(table) == -1)
                    ret = -1;
            return ret;
        }

        public override string ToString()
        {
            return "Script Name:" + title + "\r\nStatement amount:" + script_block.Count;
        }
    }

    interface IGenerate
    {
        double generate(SymbolTable table);
    }
}
