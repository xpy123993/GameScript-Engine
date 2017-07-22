using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GameEngine
{
    class SymbolTable
    {
        private Hashtable hashtable = new Hashtable();
        private string[] reserveKeyword = 
            new string[]{ "t", "dt", "gt", "x", "y", "vx", "vy", "p_x", "p_y", "bound_x", "bound_y" };

        public void setBound(double d_x, double d_y)
        {
            set("bound_x", d_x);
            set("bound_y", d_y);
        }

        private void initialize()
        {
            if (!hashtable.ContainsKey("imageFilename"))
                hashtable.Add("imageFilename", "");
            if (!hashtable.ContainsKey("prototype"))
                hashtable.Add("prototype", new List<Script>());
            if (!hashtable.ContainsKey("loadlist"))
                hashtable.Add("loadlist", new List<Script>());
        }
        public void ClearUnnecessary()
        {
            List<Script> prototype = getPrototype();
            List<Script> loadlist = getLoadList();
            double[] arr = new double[reserveKeyword.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = get(reserveKeyword[i]);
            clear();
            for (int i = 0; i < arr.Length; i++)
                set(reserveKeyword[i], arr[i]);
            hashtable.Add("prototype", prototype);
            hashtable.Add("loadlist", loadlist);
            InsertKeyState();
        }
        public bool GetKeyState(Keys keys)
        {
            return NativeMethods.GetKeyState(keys);
        }  
        public void InsertKeyState()
        {
            if (GetKeyState(Keys.Up))
                set("key_up", 0);
            else
                set("key_up", -1);

            if (GetKeyState(Keys.Down))
                set("key_down", 0);
            else
                set("key_down", -1);

            if (GetKeyState(Keys.Left))
                set("key_left", 0);
            else
                set("key_left", -1);

            if (GetKeyState(Keys.Right))
                set("key_right", 0);
            else
                set("key_right", -1);

            if (GetKeyState(Keys.Z))
                set("key_attack", 0);
            else
                set("key_attack", -1);

            if (GetKeyState(Keys.LShiftKey))
                set("key_slow", 0);
            else
                set("key_slow", -1);
            
            
        }
        
        public SymbolTable() { initialize(); }
        public SymbolTable(SymbolTable symbolTable)
        {
            hashtable = new Hashtable(symbolTable.hashtable);
            initialize();
        }
        public void clear()
        {
            hashtable.Clear();
        }
        public double get(string s)
        {
            if (!hashtable.ContainsKey(s))
            {
                hashtable.Add(s, (double)0);
            }
            return (double)hashtable[s];
        }
        public void set(string s, double c)
        {
            if (!hashtable.ContainsKey(s))
                hashtable.Add(s, c);
            else
                hashtable[s] = c;
        }
        public void setImageFilename(string filename)
        {
            if (!hashtable.ContainsKey("imageFilename"))
                hashtable.Add("imageFilename", filename);
            else
                hashtable["imageFilename"] = filename;
        }
        public string getImageFilename()
        {
            return (string)hashtable["imageFilename"];
        }
        public List<Script> getPrototype()
        {
            return (List<Script>)hashtable["prototype"];
        }

        public List<Script> getLoadList()
        {
            return (List<Script>)hashtable["loadlist"];
        }

        public bool ContainsKey(string key)
        {
            return hashtable.ContainsKey(key);
        }
    }

    class GameAgent : IDisposable
    {
        private List<Script> prototype = new List<Script>();
        private List<GameObject> currentList = new List<GameObject>();
        private ImageCache imageCache = null;
        private SymbolTable symbolTable = new SymbolTable();
        private double gameTime = 0;

        private int width = 600, height = 800;
        private double d_x, d_y, p_x, p_y;
        private int totalObjects = 0;

        public bool isPlayer = false;

        public Graphics g;
        public Brush defaultBrush = new SolidBrush(Color.White);

        private HashSet<GameObject> enemyGroup = new HashSet<GameObject>();
        private HashSet<GameObject> friendGroup = new HashSet<GameObject>();

        List<GameObject> removelist = new List<GameObject>();
        HashSet<GameObject> collideList = new HashSet<GameObject>();

        private int maxCoObject = 0;

        public GameAgent(int width, int height, Graphics g)
        {
            this.width = width;
            this.height = height;
            this.g = g;
        }

        public void setScripts(List<Script> prototype)
        {
            this.prototype = prototype;
        }

        public void initialize()
        {
            symbolTable = new SymbolTable();

            symbolTable.getPrototype().AddRange(prototype);
            currentList.Clear();
            gameTime = maxCoObject = 0;
            Script main = findScriptByName("main");
            if (main == null)
                LogWriter.WriteErrText("main unsolved");
            else
                addObject(main.clone());
            d_x = width / 2;
            d_y = height / 2;
            symbolTable.setBound(d_x, d_y);
            if (imageCache != null)
                imageCache.Dispose();
            imageCache = new ImageCache();

            enemyGroup.Clear();
            friendGroup.Clear();
            removelist.Clear();
            collideList.Clear();

        }

        public Script findScriptByName(string scriptName)
        {
            foreach (Script script in prototype)
                if (script.title.Equals(scriptName))
                    return script;
            return null;
        }

        public void addObject(Script script)
        {
            GameObject gameObject = new GameObject(script);
            currentList.Add(gameObject);
            //LogWriter.WriteLogText("[CREATE]" + script.title);
            totalObjects++;
            if (maxCoObject < currentList.Count)
                maxCoObject = currentList.Count;
        }



        public void removeObject(GameObject gameObject)
        {
            //LogWriter.WriteLogText("[DESTROY]" + gameObject.getScriptTitle());
            currentList.Remove(gameObject);
            enemyGroup.Remove(gameObject);
            friendGroup.Remove(gameObject);
        }

        public bool checkBound(GameObject gameObject)
        {
            bool flag = false;
            if (gameObject.x < -d_x)
            {
                gameObject.x = -d_x;
                flag = gameObject.OnOutOfBound();
            }
            else if (gameObject.x > d_x)
            {
                gameObject.x = d_x;
                flag = gameObject.OnOutOfBound();
            }
            if (gameObject.y < -d_y)
            {
                gameObject.y = -d_y;
                flag = gameObject.OnOutOfBound();
            }
            else if (gameObject.y > d_y)
            {
                gameObject.y = d_y;
                flag = gameObject.OnOutOfBound();
            }
            return flag;
        }

        public void checkCollide(GameObject currentObject)
        {
            if (currentObject.isFriendBullet || !currentObject.isPlayer)
            {
                HashSet<GameObject> enemies = currentObject.isFriendBullet ? enemyGroup : friendGroup;
                foreach (GameObject gameObject in enemies)
                {
                    if (gameObject.OnObjectMove(currentObject))
                    {
                        //LogWriter.WriteLogText("-" + currentObject.getScriptTitle() + ":" + gameObject.getScriptTitle());
                        collideList.Add(currentObject);
                        collideList.Add(gameObject);
                    }
                }
            }
        }

        private void drawEllipse(float x, float y, float size)
        {
            x -= size / 2;
            y -= size / 2;
            g.FillEllipse(defaultBrush, x, y, size, size);
        }

        private void drawImage(float x, float y, Image image)
        {
            x -= image.Width / 2;
            y -= image.Height / 2;
            g.DrawImage(image, x, y, image.Width, image.Height);
        }

        private void checkLoadingImage(GameObject gameObject)
        {
            if (gameObject.filename.Length > 0)
            {
                gameObject.image = imageCache.loadImage(gameObject.filename);
                gameObject.filename = "";
            }
        }

        public bool OnUpdate(double deltaTime)
        {
            gameTime += deltaTime;
            symbolTable.set("gt", gameTime);
            symbolTable.set("dt", deltaTime);
            symbolTable.set("p_x", p_x);
            symbolTable.set("p_y", p_y);
            g.Clear(Color.Black);
            double x, y;
            int ret;
            for (int i = 0; i < currentList.Count; i++)
            {
                ret = currentList[i].OnUpdate(symbolTable);
                switch (ret)
                {
                    case 1:
                        friendGroup.Add(currentList[i]);
                        break;
                    case 2:
                        enemyGroup.Add(currentList[i]);
                        break;
                    case -1:
                        removelist.Add(currentList[i]);
                        break;
                }
                if (currentList[i].isVisible && !removelist.Contains(currentList[i]))
                {
                    if (checkBound(currentList[i]) && currentList[i].OnOutOfBound())
                    {
                        removelist.Add(currentList[i]);
                        continue;
                    }

                    x = currentList[i].x + d_x;
                    y = currentList[i].y + d_y;

                    checkCollide(currentList[i]);
                    checkLoadingImage(currentList[i]);
                    if (currentList[i].image != null)
                        drawImage((float)x, (float)y, currentList[i].image);
                    else
                        drawEllipse((float)x, (float)y, (float)currentList[i].size);
                    
                    if (currentList[i].isPlayer)
                    {
                        drawEllipse((float)x, (float)y, (float)currentList[i].size);
                        p_x = currentList[i].x;
                        p_y = currentList[i].y;
                    }
                }
            }
            foreach (GameObject gameObject in removelist)
                removeObject(gameObject);
            removelist.Clear();
            foreach (GameObject gameObject in collideList)
            {
                if (gameObject.OnCollide())
                    removeObject(gameObject);
            }
            collideList.Clear();
            List<Script> loadlist = symbolTable.getLoadList();
            for (int i = 0; i < loadlist.Count; i++)
                addObject(loadlist[i]);
            loadlist.Clear();
            if (currentList.Count == 0)
            {
                OnFinished();
                initialize();
                return true;
            }
            return false;
        }

        private void OnFinished()
        {
            LogWriter.WriteLogText("Game stopped.");
            LogWriter.WriteLogText("Total time:" + gameTime.ToString("#0.00") + " s");
            LogWriter.WriteLogText("Total objects:" + totalObjects);
            LogWriter.WriteLogText("Throughput rate:" + (totalObjects / gameTime).ToString("#0.00") + " object(s)/s");
            LogWriter.WriteLogText("Max Objects amount in screen:" + maxCoObject);
            gameTime = totalObjects = 0;
        }

        public void Dispose()
        {
            defaultBrush.Dispose();
            if (imageCache != null)
                imageCache.Dispose();
        }
    }

    class GameObject
    {
        public double x = 0, y = 0, vx = 0, vy = 0;
        public double size = 10;
        public string filename = "";
        public Image image = null;
        public Script script;
        public bool isVisible = true;
        public bool isScene = false;
        public bool isPlayer = false;
        public bool isFriendBullet = false;
        public bool isEnemy = false;
        public int HP = 1;

        private double dt;
        public bool OnCollide()
        {
            HP -= 1;
            return HP == 0;
        }

        public bool OnObjectMove(GameObject gameObject)
        {
            if (!isVisible) return false;
            if (isScene || gameObject.isScene) return false;
            double r = size / 2 + gameObject.size / 2;
            double a, b;
            if ((a = Math.Abs(gameObject.x - x)) > r)
                return false;
            if ((b = Math.Abs(gameObject.y - y)) > r)
                return false;
            if (Math.Sqrt(a * a + b * b) <= r)
                return true;
            return false;
        }

        public string getScriptTitle() { return script.title; }
        public GameObject(Script script)
        {
            this.script = script;
        }

        private void setX(double x)
        {
            if (this.x == x) return;
            this.x = x;
        }

        private void setY(double y)
        {
            if (this.y == y) return;
            this.y = y;
        }

        private void setVx(double vx)
        {
            if (this.vx == vx) return;
            this.vx = vx;
        }
        private void setVy(double vy)
        {
            if (this.vy == vy) return;
            this.vy = vy;
        }
        private void setHp(int HP)
        {
            this.HP = HP;
        }
        public bool OnOutOfBound()
        {
            if (isPlayer || isScene)
                return false;
            return true;
        }
        private void SetObjectInfo(SymbolTable symbolTable)
        {
            symbolTable.set("x", x);
            symbolTable.set("y", y);
            symbolTable.set("vx", vx);
            symbolTable.set("vy", vy);
            //symbolTable.set("hp", HP);
        }
        private void GetObjectInfo(SymbolTable symbolTable)
        {
            setX(symbolTable.get("x"));
            setY(symbolTable.get("y"));
            setVx(symbolTable.get("vx"));
            setVy(symbolTable.get("vy"));
            if (symbolTable.ContainsKey("hp"))
                setHp((int) symbolTable.get("hp"));
        }
        private int CheckSpecialVar(SymbolTable symbolTable)
        {
            int ret = 0;
            String imageFilename = symbolTable.getImageFilename();
            if (imageFilename != null && imageFilename.Length > 0)
            {
                filename = imageFilename;
                symbolTable.setImageFilename("");
            }
            if (symbolTable.get("isFriendBullet") == 1)
            {
                isFriendBullet = true;
            }
            if (symbolTable.get("isPlayer") == 1)
            {
                isPlayer = true;
                ret = 1;
            }
            if (symbolTable.get("isEnemy") == 1)
            {
                isEnemy = true;
                ret = 2;
            }
            if (symbolTable.ContainsKey("isScene"))
            {
                isScene = symbolTable.get("isScene") == 1;
            }
            if (symbolTable.ContainsKey("isVisible"))
            {
                isVisible = symbolTable.get("isVisible") == 1;
            }
            return ret;
        }
        public int OnUpdate(SymbolTable symbolTable)
        {
            SetObjectInfo(symbolTable);
            if (script.generate(symbolTable) == -1)
                return -1;

            GetObjectInfo(symbolTable);

            dt = symbolTable.get("dt");

            x += dt * vx;
            y += dt * vy;

            return CheckSpecialVar(symbolTable); 
        }
    }

    class ImageCache : IDisposable
    {
        private Hashtable cache = new Hashtable();
        public Image loadImage(string filename)
        {
            if (!cache.ContainsKey(filename))
            {
                try
                {
                    Image image = Image.FromFile(filename);
                    cache.Add(filename, image);
                }
                catch (Exception)
                {
                    cache.Add(filename, null);
                    LogWriter.WriteErrText("Resource file " + filename + " unfound");
                }
            }
            
            return (Image)cache[filename];
        }

        public void Dispose()
        {
            foreach (object image in cache.Values)
                if (image != null) ((Image)image).Dispose();
        }
    }
}
