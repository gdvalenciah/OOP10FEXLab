using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace OOP10FEXLab
{
    // ---------- MODEL CLASSES ----------
    public class MenuItem
    {
        public string Category { get; }
        public string Name { get; }
        public decimal Price { get; }
        public MenuItem(string category, string name, decimal price)
        {
            Category = category; Name = name; Price = price;
        }
    }

    // One row in the cart: an item and how many
    public class CartLine
    {
        public MenuItem Item { get; }
        public int Qty { get; set; }
        public decimal Subtotal => Item.Price * Qty;
        public CartLine(MenuItem item, int qty = 1) { Item = item; Qty = qty; }
    }

    public class Order
    {
        public int Number { get; }
        public List<CartLine> Lines { get; }
        public bool IsReady { get; set; }
        public decimal Total => Lines.Sum(l => l.Subtotal);
        public Order(int number, List<CartLine> lines) { Number = number; Lines = lines; }
        public string Display => Number.ToString("000");
    }

    // Shared by both forms so they stay in sync
    public class OrderQueue
    {
        private readonly List<Order> _orders = new List<Order>();
        private int _next = 1;
        public event Action Changed;

        public IEnumerable<Order> Preparing => _orders.Where(o => !o.IsReady);
        public IEnumerable<Order> Serving => _orders.Where(o => o.IsReady);

        public Order Place(List<CartLine> lines)
        {
            var copy = lines.Select(l => new CartLine(l.Item, l.Qty)).ToList();
            var order = new Order(_next++, copy);
            _orders.Add(order);
            Changed?.Invoke();
            return order;
        }

        public void MarkReady(int number)
        {
            var o = _orders.FirstOrDefault(x => x.Number == number);
            if (o != null) { o.IsReady = true; Changed?.Invoke(); }
        }

        public void Claim(int number)
        {
            _orders.RemoveAll(x => x.Number == number);
            Changed?.Invoke();
        }
    }

    // ---------- NOTEPAD FILE STORAGE ----------
    // Reads meals from meals.txt (format: Category|Name|Price)
    public static class MealRepository
    {
        private static readonly string FilePath =
            System.IO.Path.Combine(AppContext.BaseDirectory, "meals.txt");

        public static List<MenuItem> Load()
        {
            if (!File.Exists(FilePath))
            {
                File.WriteAllLines(FilePath, new[]
                {
                    "Packages|Spaghetti + Chicken Meal|129",
                    "Packages|Spaghetti + Burger Steak|99",
                    "Packages|Burger + Drink Meal|109",
                    "Packages|Cheesy Burger + Drink Meal|119",
                    "Packages|Family Pan Spaghetti|299",
                    "Burgers|Yumburger|45",
                    "Burgers|Cheesy Yumburger|59",
                    "Chicken|1pc Chicken|89",
                    "Chicken|2pc Chicken|169",
                    "Sides|Fries|45",
                    "Sides|Burger Steak|49",
                    "Desserts|Peach Mango Pie|39",
                    "Desserts|Sundae|39",
                    "Drinks|Iced Tea|45",
                    "Drinks|Coke|40"
                });
            }

            var list = new List<MenuItem>();
            foreach (var line in File.ReadAllLines(FilePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var p = line.Split('|');
                if (p.Length == 3 && decimal.TryParse(p[2], out decimal price3))
                    list.Add(new MenuItem(p[0].Trim(), p[1].Trim(), price3));
                else if (p.Length == 2 && decimal.TryParse(p[1], out decimal price2))
                    list.Add(new MenuItem("Meals", p[0].Trim(), price2));   // old format
            }
            return list;
        }
    }

    // Saves every placed order to transactions.txt
    public static class TransactionLog
    {
        private static readonly string FilePath =
            System.IO.Path.Combine(AppContext.BaseDirectory, "transactions.txt");

        public static void Save(Order o)
        {
            string items = string.Join(", ", o.Lines.Select(l => $"{l.Qty}x {l.Item.Name}"));
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|Order {o.Display}|{items}|P{o.Total:0.00}";
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
    }

    // ---------- FOOD PICTURES ----------
    // Looks for an "images" folder and a file named after the item, e.g. "Iced Tea" -> iced-tea.png
    // If no picture is found, a colored placeholder with initials is drawn instead.
    public static class ImageLoader
    {
        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();
        private static readonly string Folder = FindFolder();
        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

        private static string FindFolder()
        {
            // search the exe folder and its parents, so the "images" folder
            // can simply sit next to Program.cs in the project
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "images");
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }

        public static string Slug(string name) =>
            Regex.Replace(name.ToLower(), "[^a-z0-9]+", "-").Trim('-');

        public static Image Get(MenuItem item, int w, int h)
        {
            string key = $"{item.Name}|{w}x{h}";
            if (Cache.TryGetValue(key, out Image cached)) return cached;
            Image img = LoadFromFile(item, w, h) ?? MakePlaceholder(item, w, h);
            Cache[key] = img;
            return img;
        }

        private static Image LoadFromFile(MenuItem item, int w, int h)
        {
            if (Folder == null) return null;
            foreach (var ext in Extensions)
            {
                string path = Path.Combine(Folder, Slug(item.Name) + ext);
                if (!File.Exists(path)) continue;
                try
                {
                    using (var src = Image.FromFile(path))
                    {
                        var bmp = new Bitmap(w, h);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.Clear(Color.White);
                            float scale = Math.Min((float)w / src.Width, (float)h / src.Height);
                            int nw = (int)(src.Width * scale), nh = (int)(src.Height * scale);
                            g.DrawImage(src, (w - nw) / 2, (h - nh) / 2, nw, nh);
                        }
                        return bmp;
                    }
                }
                catch { return null; }
            }
            return null;
        }

        private static Image MakePlaceholder(MenuItem item, int w, int h)
        {
            var palette = new[] { Color.Tomato, Color.Orange, Color.SeaGreen, Color.SteelBlue, Color.MediumPurple, Color.Goldenrod };
            Color color = palette[item.Category.Sum(c => (int)c) % palette.Length];
            string initials = string.Concat(item.Name
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => char.IsLetterOrDigit(p[0]))
                .Take(2)
                .Select(p => char.ToUpper(p[0])));

            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", h / 2.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(color);
                g.DrawString(initials, font, Brushes.White, new RectangleF(0, 0, w, h), sf);
            }
            return bmp;
        }
    }

    // ---------- KIOSK UI ----------
    public class KioskForm : Form
    {
        // Fast-food colors
        private static readonly Color Red = Color.FromArgb(200, 16, 46);
        private static readonly Color DarkRed = Color.FromArgb(140, 10, 30);
        private static readonly Color Yellow = Color.FromArgb(255, 199, 44);
        private static readonly Color Cream = Color.FromArgb(255, 248, 225);

        private readonly OrderQueue _queue;
        private readonly List<MenuItem> _menu;
        private readonly List<CartLine> _cart = new List<CartLine>();
        private readonly FlowLayoutPanel _menuPanel = new FlowLayoutPanel();
        private readonly FlowLayoutPanel _catPanel = new FlowLayoutPanel();
        private readonly ListView _cartView = new ListView();
        private readonly Label _totalLabel = new Label();
        private readonly ImageList _cartImages = new ImageList { ImageSize = new Size(48, 36), ColorDepth = ColorDepth.Depth32Bit };
        private string _selectedCategory;

        public KioskForm(OrderQueue queue)
        {
            _queue = queue;
            _menu = MealRepository.Load();   // meals come from meals.txt

            Text = "KIOSK - Place your order";
            Size = new Size(1000, 620);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(20, 40);
            BackColor = Cream;

            // Header
            var header = new Label
            {
                Text = "ORDER HERE  -  TAP YOUR MEAL",
                Dock = DockStyle.Top,
                Height = 70,
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Red,
                ForeColor = Yellow
            };

            // Menu area (big buttons)
            _menuPanel.Dock = DockStyle.Fill;
            _menuPanel.Padding = new Padding(10);
            _menuPanel.AutoScroll = true;
            _menuPanel.BackColor = Cream;

            // Category sidebar
            _catPanel.Dock = DockStyle.Left;
            _catPanel.Width = 160;
            _catPanel.FlowDirection = FlowDirection.TopDown;
            _catPanel.WrapContents = false;
            _catPanel.Padding = new Padding(10);
            _catPanel.BackColor = DarkRed;

            // Cart area
            var cartPanel = new Panel { Dock = DockStyle.Right, Width = 340, Padding = new Padding(10), BackColor = Cream };

            var cartTitle = new Label
            {
                Text = "YOUR ORDER",
                Dock = DockStyle.Top,
                Height = 40,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Red
            };

            _cartView.Dock = DockStyle.Fill;
            _cartView.View = View.Details;
            _cartView.FullRowSelect = true;
            _cartView.HideSelection = false;
            _cartView.GridLines = true;
            _cartView.Font = new Font("Segoe UI", 11);
            _cartView.SmallImageList = _cartImages;   // pictures in the cart rows
            _cartView.Columns.Add("Item", 165);
            _cartView.Columns.Add("Qty", 45);
            _cartView.Columns.Add("Subtotal", 85);

            var qtyPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
            var btnPlus = MakeButton("+", Yellow, DarkRed, 72, 40, 14);
            var btnMinus = MakeButton("-", Yellow, DarkRed, 72, 40, 14);
            var btnRemove = MakeButton("Remove", Red, Color.White, 72, 40, 9);
            var btnClear = MakeButton("Clear", DarkRed, Color.White, 72, 40, 9);
            btnPlus.Click += (s, e) => ChangeQty(1);
            btnMinus.Click += (s, e) => ChangeQty(-1);
            btnRemove.Click += (s, e) => RemoveSelected();
            btnClear.Click += (s, e) => ClearCart();
            qtyPanel.Controls.AddRange(new Control[] { btnPlus, btnMinus, btnRemove, btnClear });

            _totalLabel.Dock = DockStyle.Bottom;
            _totalLabel.Height = 45;
            _totalLabel.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            _totalLabel.ForeColor = Red;
            _totalLabel.TextAlign = ContentAlignment.MiddleRight;
            _totalLabel.Text = "Total: P0.00";

            var btnPlace = MakeButton("PLACE ORDER", Red, Yellow, 100, 60, 14);
            btnPlace.Dock = DockStyle.Bottom;
            btnPlace.Click += (s, e) => PlaceOrder();

            // add order matters for docking: Fill first, bottom items, then top
            cartPanel.Controls.Add(_cartView);
            cartPanel.Controls.Add(qtyPanel);
            cartPanel.Controls.Add(_totalLabel);
            cartPanel.Controls.Add(btnPlace);
            cartPanel.Controls.Add(cartTitle);

            Controls.Add(_menuPanel);
            Controls.Add(_catPanel);
            Controls.Add(cartPanel);
            Controls.Add(header);

            _selectedCategory = _menu.Select(m => m.Category).FirstOrDefault();
            BuildCategories();
            BuildMenu();
        }

        private static Button MakeButton(string text, Color back, Color fore, int w, int h, float size)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(w, h),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        // ----- categories -----
        private void BuildCategories()
        {
            _catPanel.Controls.Clear();
            foreach (var cat in _menu.Select(m => m.Category).Distinct())
            {
                bool active = cat == _selectedCategory;
                var btn = MakeButton(cat.ToUpper(),
                    active ? Yellow : Red,
                    active ? DarkRed : Color.White, 135, 60, 11);
                string captured = cat;
                btn.Click += (s, e) =>
                {
                    _selectedCategory = captured;
                    BuildCategories();
                    BuildMenu();
                };
                _catPanel.Controls.Add(btn);
            }
        }

        // ----- big meal buttons -----
        private void BuildMenu()
        {
            _menuPanel.Controls.Clear();
            foreach (var item in _menu.Where(m => m.Category == _selectedCategory))
            {
                var btn = MakeButton($"{item.Name}\nP{item.Price:0.00}", Yellow, DarkRed, 180, 170, 10);
                btn.Image = ImageLoader.Get(item, 130, 90);
                btn.ImageAlign = ContentAlignment.TopCenter;
                btn.TextAlign = ContentAlignment.BottomCenter;
                var captured = item;
                btn.Click += (s, e) => AddToCart(captured);
                _menuPanel.Controls.Add(btn);
            }
        }

        // ----- cart logic -----
        private int SelectedIndex => _cartView.SelectedIndices.Count > 0 ? _cartView.SelectedIndices[0] : -1;

        private void AddToCart(MenuItem item)
        {
            var line = _cart.FirstOrDefault(l => l.Item == item);
            if (line == null) { _cart.Add(new CartLine(item)); RefreshCart(_cart.Count - 1); }
            else { line.Qty++; RefreshCart(_cart.IndexOf(line)); }
        }

        private void ChangeQty(int delta)
        {
            int idx = SelectedIndex;
            if (idx < 0) return;
            _cart[idx].Qty += delta;
            if (_cart[idx].Qty <= 0) { _cart.RemoveAt(idx); idx = Math.Min(idx, _cart.Count - 1); }
            RefreshCart(idx);
        }

        private void RemoveSelected()
        {
            int idx = SelectedIndex;
            if (idx < 0) return;
            _cart.RemoveAt(idx);
            RefreshCart(Math.Min(idx, _cart.Count - 1));
        }

        private void ClearCart() { _cart.Clear(); RefreshCart(-1); }

        private void RefreshCart(int select)
        {
            _cartView.Items.Clear();
            foreach (var l in _cart)
            {
                string key = l.Item.Name;
                if (!_cartImages.Images.ContainsKey(key))
                    _cartImages.Images.Add(key, ImageLoader.Get(l.Item, 48, 36));
                _cartView.Items.Add(new ListViewItem(new[] { l.Item.Name, l.Qty.ToString(), $"P{l.Subtotal:0.00}" }, key));
            }
            _totalLabel.Text = $"Total: P{_cart.Sum(l => l.Subtotal):0.00}";
            if (select >= 0 && select < _cartView.Items.Count)
                _cartView.Items[select].Selected = true;
        }

        private void PlaceOrder()
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Please add at least one item.");
                return;
            }
            var order = _queue.Place(_cart);
            TransactionLog.Save(order);   // record the transaction
            MessageBox.Show($"Order placed!\nYour number: {order.Display}\nTotal: P{order.Total:0.00}", "Thank you");
            ClearCart();
        }
    }

    // ---------- QUEUE MONITOR UI ----------
    public class MonitorForm : Form
    {
        private readonly OrderQueue _queue;
        private readonly ListBox _preparing = new ListBox();
        private readonly ListBox _serving = new ListBox();

        public MonitorForm(OrderQueue queue)
        {
            _queue = queue;
            Text = "CLAIM MONITOR";
            Size = new Size(600, 520);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(1030, 80);
            BackColor = Color.DodgerBlue;

            var title = new Label
            {
                Text = "CLAIM MONITOR",
                Dock = DockStyle.Top,
                Height = 60,
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White
            };

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            table.Controls.Add(MakeHeader("PREPARING"), 0, 0);
            table.Controls.Add(MakeHeader("NOW SERVING"), 1, 0);
            StyleList(_preparing);
            StyleList(_serving);
            table.Controls.Add(_preparing, 0, 1);
            table.Controls.Add(_serving, 1, 1);

            var hint = new Label
            {
                Text = "Double-click PREPARING = ready | Double-click SERVING = claimed",
                Dock = DockStyle.Bottom,
                Height = 30,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(table);
            Controls.Add(hint);
            Controls.Add(title);

            _preparing.DoubleClick += (s, e) => { if (_preparing.SelectedItem != null) _queue.MarkReady(int.Parse(_preparing.SelectedItem.ToString())); };
            _serving.DoubleClick += (s, e) => { if (_serving.SelectedItem != null) _queue.Claim(int.Parse(_serving.SelectedItem.ToString())); };

            _queue.Changed += RefreshLists;   // auto-update when queue changes
            RefreshLists();
        }

        private Label MakeHeader(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.Yellow,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        private void StyleList(ListBox lb)
        {
            lb.Dock = DockStyle.Fill;
            lb.Font = new Font("Segoe UI", 28, FontStyle.Bold);
            lb.BackColor = Color.DodgerBlue;
            lb.ForeColor = Color.White;
            lb.BorderStyle = BorderStyle.None;
        }

        private void RefreshLists()
        {
            _preparing.Items.Clear();
            _serving.Items.Clear();
            foreach (var o in _queue.Preparing) _preparing.Items.Add(o.Display);
            foreach (var o in _queue.Serving) _serving.Items.Add(o.Display);
        }
    }

    // ---------- ENTRY POINT ----------
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            var queue = new OrderQueue();          // ONE shared queue
            var monitor = new MonitorForm(queue);
            var kiosk = new KioskForm(queue);
            monitor.Show();
            Application.Run(kiosk);
        }
    }
}
