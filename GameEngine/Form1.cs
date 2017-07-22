using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections;
using System.IO;

namespace GameEngine
{
    public partial class Form1 : Form
    {

        SymbolTable symbolTable = new SymbolTable();
        ScriptAnalyser scriptAnalyser = new ScriptAnalyser();
        GameAgent gameAgent;

        Image gameImage;
        Graphics g;

        bool isDrawing = false;

        int skipFrameCount = 0;


        public Form1()
        {
            InitializeComponent();
        }

        private void load_temp_code(string filename)
        {
            FileStream fileStream = new FileStream(filename, FileMode.OpenOrCreate);
            StreamReader reader = new StreamReader(fileStream);
            string temp = "", line;
            while ((line = reader.ReadLine()) != null)
                if (line.Length > 0) temp += line + "\r\n";
            codeEditor.Text = temp;
            reader.Close();
        }

        private void save_temp_code(string filename)
        {
            FileStream fileStream = new FileStream(filename, FileMode.Create);
            StreamWriter writer = new StreamWriter(fileStream);
            writer.Write(codeEditor.Text);
            writer.Close();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            load_temp_code("temp.txt");

            gameImage = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            g = Graphics.FromImage(gameImage);
            g.Clear(Color.Black);
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            gameAgent = new GameAgent(gameImage.Width, gameImage.Height, g);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            textBox2.Text = "";
            List<Script> game_script = scriptAnalyser.scan(codeEditor.Text);

            gameAgent.setScripts(game_script);
            resetGameWindow();
            gameAgent.initialize();
            
            showLogText();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (button2.Text.Equals("Start"))
            {
                button1_Click(sender, e);
                button2.Text = "Stop";
                startGame();
            }
            else
                OnGameStop();
        }

        private void startGame()
        {
            
            timer1.Interval = 20;
            timer1.Enabled = true;
            textBox2.ReadOnly = true;
            codeEditor.ReadOnly = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            drawGameWindow();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            save_temp_code("temp.txt");
            
        }

        private void resetGameWindow()
        {
            timer1.Enabled = false;
            isDrawing = false;
            skipFrameCount = 0;
            button2.Text = "Start";
            codeEditor.ReadOnly = textBox2.ReadOnly = false;
        }

        private void OnGameStop()
        {
            LogWriter.WriteLogText("Skipped " + skipFrameCount + " frame(s)");
            resetGameWindow();
            g.Clear(Color.Black);
            pictureBox1.Refresh();
        }

        private void showLogText()
        {
            string log = LogWriter.logText;
            LogWriter.logText = "";
            if (log.Length > 0)
            {
                textBox2.Text += log;
                if (textBox2.Text != null && textBox2.Text.Length > 0)
                {
                    textBox2.SelectionStart = textBox2.Text.Length - 1;
                    textBox2.ScrollToCaret();
                }
            }
        }

        private void showGameImage()
        {
            pictureBox1.Image = gameImage;
        }

        private void drawGameWindow()
        {
            if (isDrawing)
            {
                skipFrameCount++;
                return;
            }
            isDrawing = true;

            if (gameAgent.OnUpdate(timer1.Interval / 1000.0))
                OnGameStop();

            showLogText();
            showGameImage();

            isDrawing = false;
        }
    }

    public class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetKeyState")]
        private static extern short GetKeyState(
            int nVirtKey // Long，欲测试的虚拟键键码。对字母、数字字符（A-Z、a-z、0-9），用它们实际的ASCII值  
        );
        public static bool GetKeyState(Keys keys)
        {
            return ((GetKeyState((int)keys) & 0x8000) != 0) ? true : false;
        }
    }

    public interface Drawable
    {
        void Draw(Graphics g, int width, int height);
    }
}
