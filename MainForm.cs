using System;
using System.Data;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Printing;
using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MatrimonialApp
{
    public class MainForm : Form
    {
        // UI
        TextBox txtName, txtFather, txtCity, txtPhone, txtEducation, txtReligion, txtCaste,
                txtOccupation, txtMarital, txtAddress, txtSearch;
        PictureBox pic;
        DataGridView grid;
        Button btnAdd, btnUpdate, btnDelete, btnNew, btnBrowse, btnPrint, btnPdf, btnTheme;

        string DbFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "records.db");
        string ConnStr => $"Data Source={DbFile}";
        string imagePath = "";
        Color? userAccent;

        public MainForm()
        {
            Text = "Matrimonial Records (اردو + English)";
            Width = 1200; Height = 750;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);

            CreateUi();
            EnsureDb();
            LoadData();
        }

        // ---------- UI ----------
        void CreateUi()
        {
            // سرچ
            var lblSearch = L("Search / تلاش:");
            lblSearch.Left = 20; lblSearch.Top = 15;
            txtSearch = new TextBox { Left = 120, Top = 12, Width = 420 };
            txtSearch.TextChanged += (s, e) => LoadData(txtSearch.Text);

            // گرِڈ
            grid = new DataGridView
            {
                Left = 20, Top = 45, Width = 1140, Height = 260,
                ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.SelectionChanged += Grid_SelectionChanged;

            // تصویر
            pic = new PictureBox { Left = 20, Top = 325, Width = 160, Height = 190, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            btnBrowse = B("تصویر منتخب کریں / Browse", 20, 520, 160, 34, (s,e)=>BrowseImage());

            // فارم فیلڈز
            int x = 200, y = 320, w = 360, gap = 40;
            AddField("Name / نام:", ref txtName, x, ref y, w, gap);
            AddField("Father's Name / ولدیت:", ref txtFather, x, ref y, w, gap);
            AddField("City / شہر:", ref txtCity, x, ref y, w, gap);
            AddField("Phone / فون:", ref txtPhone, x, ref y, w, gap);
            AddField("Education / تعلیم:", ref txtEducation, x, ref y, w, gap);
            AddField("Religion / مذہب:", ref txtReligion, x, ref y, w, gap);
            AddField("Caste / ذات:", ref txtCaste, x, ref y, w, gap);

            int x2 = 600; y = 320;
            AddField("Father's Occupation / والد کا پیشہ:", ref txtOccupation, x2, ref y, w, gap);
            AddField("Marital Status / ازدواجی حیثیت:", ref txtMarital, x2, ref y, w, gap);
            AddField("Address / پتہ:", ref txtAddress, x2, ref y, w, gap);

            // بٹن
            int bx = 200, by = 600, bw = 125, bh = 36, space = 10;
            btnAdd    = B("Add / شامل کریں",  bx,                     by, bw, bh, (s,e)=>AddRecord());
            btnUpdate = B("Edit / تبدیلی",     bx+(bw+space),          by, bw, bh, (s,e)=>UpdateRecord());
            btnDelete = B("Delete / حذف",      bx+2*(bw+space),        by, bw, bh, (s,e)=>DeleteRecord());
            btnNew    = B("New / نیا فارم",    bx+3*(bw+space),        by, bw, bh, (s,e)=>ClearForm());
            btnPrint  = B("Print / پرنٹ",      bx+4*(bw+space),        by, bw, bh, (s,e)=>PrintRecord());
            btnPdf    = B("Export PDF",        bx+5*(bw+space),        by, bw, bh, (s,e)=>ExportPdf());
            btnTheme  = B("Theme / رنگ",       bx+6*(bw+space),        by, bw, bh, (s,e)=>PickTheme());

            Controls.AddRange(new Control[] { lblSearch, txtSearch, grid, pic, btnBrowse,
                                              btnAdd, btnUpdate, btnDelete, btnNew, btnPrint, btnPdf, btnTheme });
        }

        Label L(string t) => new Label { Text = t, AutoSize = true };
        Button B(string t, int l, int tp, int w, int h, EventHandler onClick)
        {
            var b = new Button { Text = t, Left = l, Top = tp, Width = w, Height = h };
            b.Click += onClick;
            return b;
        }
        void AddField(string caption, ref TextBox box, int x, ref int y, int w, int gap)
        {
            var lbl = L(caption); lbl.Left = x; lbl.Top = y; Controls.Add(lbl);
            box = new TextBox { Left = x + 230, Top = y - 4, Width = w };
            Controls.Add(box); y += gap;
        }

        // ---------- DB ----------
        void EnsureDb()
        {
            if (!File.Exists(DbFile))
                SqliteConnection.CreateFile(DbFile);

            using var con = new SqliteConnection(ConnStr);
            con.Open();
            var sql = @"
CREATE TABLE IF NOT EXISTS Users(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT,
    FatherName TEXT,
    City TEXT,
    Phone TEXT,
    Education TEXT,
    Religion TEXT,
    Caste TEXT,
    Occupation TEXT,
    MaritalStatus TEXT,
    Address TEXT,
    ImagePath TEXT,
    CreatedAt TEXT
);";
            new SqliteCommand(sql, con).ExecuteNonQuery();
        }

        void LoadData(string search = "")
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText =
                @"SELECT Id,Name,FatherName,City,Phone,Education,Religion,Caste,Occupation,MaritalStatus,Address,ImagePath
                  FROM Users
                  WHERE @q='' OR (Name LIKE @like OR Phone LIKE @like OR City LIKE @like OR Caste LIKE @like)
                  ORDER BY Id DESC;";
            cmd.Parameters.AddWithValue("@q", search ?? "");
            cmd.Parameters.AddWithValue("@like", $"%{search}%");
            var dt = new DataTable();
            using var da = new SqliteDataAdapter(cmd);
            da.Fill(dt);
            grid.DataSource = dt;
            grid.Columns["ImagePath"].Visible = false;
        }

        // ---------- CRUD ----------
        void AddRecord()
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"INSERT INTO Users
(Name,FatherName,City,Phone,Education,Religion,Caste,Occupation,MaritalStatus,Address,ImagePath,CreatedAt)
VALUES(@n,@f,@c,@p,@e,@r,@ca,@o,@m,@a,@img,@t);";
            cmd.Parameters.AddWithValue("@n", txtName.Text);
            cmd.Parameters.AddWithValue("@f", txtFather.Text);
            cmd.Parameters.AddWithValue("@c", txtCity.Text);
            cmd.Parameters.AddWithValue("@p", txtPhone.Text);
            cmd.Parameters.AddWithValue("@e", txtEducation.Text);
            cmd.Parameters.AddWithValue("@r", txtReligion.Text);
            cmd.Parameters.AddWithValue("@ca", txtCaste.Text);
            cmd.Parameters.AddWithValue("@o", txtOccupation.Text);
            cmd.Parameters.AddWithValue("@m", txtMarital.Text);
            cmd.Parameters.AddWithValue("@a", txtAddress.Text);
            cmd.Parameters.AddWithValue("@img", SavePhotoIfAny());
            cmd.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
            LoadData(txtSearch.Text);
            ClearForm();
            MessageBox.Show("ریکارڈ شامل ہو گیا");
        }

        void UpdateRecord()
        {
            if (grid.CurrentRow == null) return;
            var id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);

            using var con = new SqliteConnection(ConnStr);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"UPDATE Users SET
Name=@n,FatherName=@f,City=@c,Phone=@p,Education=@e,Religion=@r,Caste=@ca,Occupation=@o,MaritalStatus=@m,Address=@a,ImagePath=@img
WHERE Id=@id;";
            cmd.Parameters.AddWithValue("@n", txtName.Text);
            cmd.Parameters.AddWithValue("@f", txtFather.Text);
            cmd.Parameters.AddWithValue("@c", txtCity.Text);
            cmd.Parameters.AddWithValue("@p", txtPhone.Text);
            cmd.Parameters.AddWithValue("@e", txtEducation.Text);
            cmd.Parameters.AddWithValue("@r", txtReligion.Text);
            cmd.Parameters.AddWithValue("@ca", txtCaste.Text);
            cmd.Parameters.AddWithValue("@o", txtOccupation.Text);
            cmd.Parameters.AddWithValue("@m", txtMarital.Text);
            cmd.Parameters.AddWithValue("@a", txtAddress.Text);
            cmd.Parameters.AddWithValue("@img", SavePhotoIfAny());
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadData(txtSearch.Text);
            MessageBox.Show("ریکارڈ اپڈیٹ ہو گیا");
        }

        void DeleteRecord()
        {
            if (grid.CurrentRow == null) return;
            var id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            if (MessageBox.Show("حذف کریں؟", "Confirm", MessageBoxButtons.YesNo) == DialogResult.No) return;

            using var con = new SqliteConnection(ConnStr);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM Users WHERE Id=@id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadData(txtSearch.Text);
            ClearForm();
            MessageBox.Show("ریکارڈ حذف ہو گیا");
        }

        void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (grid.CurrentRow == null) return;
            txtName.Text       = grid.CurrentRow.Cells["Name"]?.Value?.ToString();
            txtFather.Text     = grid.CurrentRow.Cells["FatherName"]?.Value?.ToString();
            txtCity.Text       = grid.CurrentRow.Cells["City"]?.Value?.ToString();
            txtPhone.Text      = grid.CurrentRow.Cells["Phone"]?.Value?.ToString();
            txtEducation.Text  = grid.CurrentRow.Cells["Education"]?.Value?.ToString();
            txtReligion.Text   = grid.CurrentRow.Cells["Religion"]?.Value?.ToString();
            txtCaste.Text      = grid.CurrentRow.Cells["Caste"]?.Value?.ToString();
            txtOccupation.Text = grid.CurrentRow.Cells["Occupation"]?.Value?.ToString();
            txtMarital.Text    = grid.CurrentRow.Cells["MaritalStatus"]?.Value?.ToString();
            txtAddress.Text    = grid.CurrentRow.Cells["Address"]?.Value?.ToString();

            var img = grid.CurrentRow.Cells["ImagePath"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(img) && File.Exists(img))
            {
                pic.Image = Image.FromFile(img);
                imagePath = img;
            }
            else { pic.Image = null; imagePath = ""; }
        }

        void ClearForm()
        {
            foreach (Control c in Controls)
                if (c is TextBox t && t != txtSearch) t.Text = "";
            pic.Image = null;
            imagePath = "";
        }

        string SavePhotoIfAny()
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return "";
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photos");
                Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, $"{Guid.NewGuid()}{Path.GetExtension(imagePath)}");
                File.Copy(imagePath, dest, true);
                return dest;
            }
            catch { return ""; }
        }

        void BrowseImage()
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                imagePath = ofd.FileName;
                pic.Image = Image.FromFile(imagePath);
            }
        }

        // ---------- Print ----------
        void PrintRecord()
        {
            if (grid.CurrentRow == null) { MessageBox.Show("کوئی ریکارڈ منتخب کریں"); return; }
            var doc = new PrintDocument();
            doc.PrintPage += (s, e) =>
            {
                float x = 50, y = 60, lh = 28;
                using var title = new Font("Segoe UI", 16, FontStyle.Bold);
                using var f = new Font("Segoe UI", 11);

                e.Graphics.DrawString("Matrimonial Record / ریکارڈ", title, Brushes.Black, x, y);
                y += 50;

                void Line(string label, string val){ e.Graphics.DrawString($"{label}: {val}", f, Brushes.Black, x, y); y += lh; }

                Line("Name / نام", txtName.Text);
                Line("Father / ولدیت", txtFather.Text);
                Line("City / شہر", txtCity.Text);
                Line("Phone / فون", txtPhone.Text);
                Line("Education / تعلیم", txtEducation.Text);
                Line("Religion / مذہب", txtReligion.Text);
                Line("Caste / ذات", txtCaste.Text);
                Line("Occupation / پیشہ", txtOccupation.Text);
                Line("Marital / ازدواجی حیثیت", txtMarital.Text);
                Line("Address / پتہ", txtAddress.Text);

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    e.Graphics.DrawImage(Image.FromFile(imagePath), new Rectangle(700, 80, 160, 200));
            };
            using var preview = new PrintPreviewDialog { Document = doc, Width = 1000, Height = 700 };
            preview.ShowDialog();
        }

        // ---------- PDF ----------
        void ExportPdf()
        {
            if (grid.CurrentRow == null) { MessageBox.Show("کوئی ریکارڈ منتخب کریں"); return; }
            var sfd = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "Record.pdf" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("Matrimonial Record / ریکارڈ")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(180); c.RelativeColumn(); });

                            void Row(string k, string v){ t.Cell().Element(Key).Text(k); t.Cell().Element(Val).Text(v); }
                            IContainer Key(IContainer x)=>x.Background(Colors.Grey.Lighten3).Padding(6);
                            IContainer Val(IContainer x)=>x.BorderBottom(1).Padding(6);

                            Row("Name / نام", txtName.Text);
                            Row("Father's Name / ولدیت", txtFather.Text);
                            Row("City / شہر", txtCity.Text);
                            Row("Phone / فون", txtPhone.Text);
                            Row("Education / تعلیم", txtEducation.Text);
                            Row("Religion / مذہب", txtReligion.Text);
                            Row("Caste / ذات", txtCaste.Text);
                            Row("Occupation / پیشہ", txtOccupation.Text);
                            Row("Marital Status / ازدواجی حیثیت", txtMarital.Text);
                            Row("Address / پتہ", txtAddress.Text);
                        });

                        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                            col.Item().PaddingTop(10).AlignRight().Width(180).Height(220)
                               .Image(imagePath, ImageScaling.FitArea);
                    });

                    page.Footer().AlignCenter().Text(txt => {
                        txt.Span("Generated by MatrimonialApp ").FontColor(Colors.Grey.Medium);
                        txt.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    });
                });
            }).GeneratePdf(sfd.FileName);

            MessageBox.Show("PDF بن گیا!");
        }

        // ---------- Theme ----------
        void PickTheme()
        {
            using var cd = new ColorDialog();
            if (cd.ShowDialog() == DialogResult.OK)
            {
                userAccent = cd.Color;
                ApplyTheme();
            }
        }
        void ApplyTheme()
        {
            var baseColor = userAccent ?? Color.FromArgb(245, 248, 255);
            BackColor = baseColor;
            foreach (Control c in Controls)
                if (c is Button b) b.BackColor = ControlPaint.Light(baseColor);
        }
    }
}
