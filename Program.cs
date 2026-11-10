using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OOP10FEXLab
{
    // ---------- MODEL CLASSES ----------
    public class MenuItem
    {
        public string Name { get; }
        public decimal Price { get; }
        public MenuItem(string name, decimal price) { Name = name; Price = price; }
        public override string ToString() => $"{Name} - P{Price:0.00}";
    }

    public class Order
    {
        public int Number { get; }
        public List<MenuItem> Items { get; }
        public bool IsReady { get; set; }
        public decimal Total => Items.Sum(i => i.Price);
        public Order(int number, List<MenuItem> items) { Number = number; Items = items; }
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

        public Order Place(List<MenuItem> items)
        {
            var order = new Order(_next++, new List<MenuItem>(items));
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
    // Reads package meals from meals.txt (format: Name|Price)
    public static class MealRepository
    {
        private static readonly string Path =
            System.IO.Path.Combine(AppContext.BaseDirectory, "meals.txt");

        public static List<MenuItem> Load()
        {
            if (!System.IO.File.Exists(Path))
            {
                // create a default file if it is missing
                System.IO.File.WriteAllLines(Path, new[]
                {
                    "S1 Jolly Spaghetti with Chickenjoy|129",
                    "S2 Jolly Spaghetti with Burger Steak|99",
                    "S3 Jolly Spaghetti with Yumburger and Drink|109",
                    "S4 Jolly Spaghetti with Cheesy Yumburger and Drink|119",
                    "Double Jolly Spaghetti Family Pan|299",
                    "Peach Mango Pie|39",
                    "Iced Tea|45",
                    "Sundae|39"
                });
            }

            var list = new List<MenuItem>();
            foreach (var line in System.IO.File.ReadAllLines(Path))
            {
                var parts = line.Split('|');
                if (parts.Length == 2 && decimal.TryParse(parts[1], out decimal price))
                    list.Add(new MenuItem(parts[0].Trim(), price));
            }
            return list;
        }
    }

    // Saves every placed order to transactions.txt
    public static class TransactionLog
    {
        private static readonly string Path =
            System.IO.Path.Combine(AppContext.BaseDirectory, "transactions.txt");

        public static void Save(Order o)
        {
            string items = string.Join(", ", o.Items.Select(i => i.Name));
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|Order {o.Display}|{items}|P{o.Total:0.00}";
            System.IO.File.AppendAllText(Path, line + Environment.NewLine);
        }
    }

    // ---------- KIOSK UI ----------
    public class KioskForm : Form
    {
        private readonly OrderQueue _queue;
        private readonly List<MenuItem> _cart = new List<MenuItem>();
        private readonly ListBox _cartList = new ListBox();
        private readonly Label _totalLabel = new Label();

        private readonly List<MenuItem> _menu;

        public KioskForm(OrderQueue queue)
        {
            _queue = queue;
            _menu = MealRepository.Load();   // package meals come from meals.txt
            Text = "KIOSK - Place your order";
            Size = new Size(800, 520);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(50, 80);

            var title = new Label
            {
                Text = "WELCOME! TAP TO ORDER",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.OrangeRed,
                ForeColor = Color.White
            };

            // Menu buttons on the left
            var menuPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            foreach (var item in _menu)
            {
                var btn = new Button
                {
                    Text = $"{item.Name}\nP{item.Price:0.00}",
                    Size = new Size(130, 90),
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    BackColor = Color.Gold
                };
                var captured = item;
                btn.Click += (s, e) => AddToCart(captured);
                menuPanel.Controls.Add(btn);
            }

            // Cart on the right
            var cartPanel = new Panel { Dock = DockStyle.Right, Width = 280, Padding = new Padding(10) };
            var cartTitle = new Label { Text = "YOUR ORDER", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            _cartList.Dock = DockStyle.Fill;
            _cartList.Font = new Font("Segoe UI", 11);

            _totalLabel.Dock = DockStyle.Bottom; _totalLabel.Height = 35;
            _totalLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            _totalLabel.Text = "Total: P0.00";

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 55 };
            var btnPlace = new Button { Text = "PLACE ORDER", Size = new Size(130, 45), BackColor = Color.LimeGreen, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            var btnClear = new Button { Text = "CLEAR", Size = new Size(100, 45), BackColor = Color.IndianRed, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnPlace.Click += (s, e) => PlaceOrder();
            btnClear.Click += (s, e) => ClearCart();
            btnPanel.Controls.Add(btnPlace);
            btnPanel.Controls.Add(btnClear);

            cartPanel.Controls.Add(_cartList);
            cartPanel.Controls.Add(btnPanel);
            cartPanel.Controls.Add(_totalLabel);
            cartPanel.Controls.Add(cartTitle);

            Controls.Add(menuPanel);
            Controls.Add(cartPanel);
            Controls.Add(title);
        }

        private void AddToCart(MenuItem item)
        {
            _cart.Add(item);
            RefreshCart();
        }

        private void RefreshCart()
        {
            _cartList.Items.Clear();
            foreach (var i in _cart) _cartList.Items.Add(i.ToString());
            _totalLabel.Text = $"Total: P{_cart.Sum(i => i.Price):0.00}";
        }

        private void ClearCart() { _cart.Clear(); RefreshCart(); }

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
            Size = new Size(700, 520);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(880, 80);
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
                Text = "Double-click a PREPARING number = ready | Double-click a SERVING number = claimed",
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
            ForeColor = Color.White,
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
