using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace WpfApp18
{
    public partial class MainWindow : Window
    {
        private string connectionString = "Host=localhost;Port=5432;Database=SalesDB;Username=postgres;Password=sa";
        private DateTime? currentFilterStart = null;
        private DateTime? currentFilterEnd = null;

        public MainWindow()
        {
            InitializeComponent();
            LoadProducts();
            LoadSales();
            LoadSelectors();
            UpdateStatistics();
            UpdateStatus("Приложение готово к работе");
        }

        private void LoadProducts()
        {
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"SELECT p.Id as ID, p.Name as Название, c.Name as Категория, p.Price as Цена 
                      FROM Products p
                      LEFT JOIN Categories c ON p.CategoryId = c.Id
                      ORDER BY p.Name";
                    DataTable dt = new DataTable();
                    using (var adapter = new NpgsqlDataAdapter(sql, conn))
                    {
                        adapter.Fill(dt);
                    }
                    ProductsGrid.ItemsSource = dt.DefaultView;
                    UpdateStatus($"Загружено {dt.Rows.Count} товаров");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки товаров: {ex.Message}");
            }
        }

        private void LoadSales()
        {
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"SELECT s.Id as ID, p.Name as Товар, cl.FullName as Клиент, 
                      s.Quantity as Колво, p.Price as Цена, 
                      (p.Price * s.Quantity) as Сумма, s.SaleDate as Дата
                      FROM Sales s
                      JOIN Products p ON s.ProductId = p.Id
                      JOIN Clients cl ON s.ClientId = cl.Id";

                    if (currentFilterStart.HasValue && currentFilterEnd.HasValue)
                    {
                        sql += $" WHERE s.SaleDate BETWEEN '{currentFilterStart.Value:yyyy-MM-dd}' AND '{currentFilterEnd.Value:yyyy-MM-dd}'";
                    }
                    sql += " ORDER BY s.SaleDate DESC";

                    DataTable dt = new DataTable();
                    using (var adapter = new NpgsqlDataAdapter(sql, conn))
                    {
                        adapter.Fill(dt);
                    }
                    SalesGrid.ItemsSource = dt.DefaultView;
                    UpdateStatus($"Загружено {dt.Rows.Count} продаж");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки продаж: {ex.Message}");
            }
        }

        private void LoadSelectors()
        {
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();

                    var categories = new List<CategoryItem>();
                    string categorySql = "SELECT Id, Name FROM Categories ORDER BY Name";
                    using (var cmd = new NpgsqlCommand(categorySql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            categories.Add(new CategoryItem { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                        }
                    }
                    CategorySelect.ItemsSource = categories;
                    CategorySelect.DisplayMemberPath = "Name";
                    CategorySelect.SelectedValuePath = "Id";

                    var products = new List<ProductItem>();
                    string productSql = "SELECT Id, Name FROM Products ORDER BY Name";
                    using (var cmd = new NpgsqlCommand(productSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            products.Add(new ProductItem { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                        }
                    }
                    ProductSelect.ItemsSource = products;
                    ProductSelect.DisplayMemberPath = "Name";
                    ProductSelect.SelectedValuePath = "Id";

                    var clients = new List<ClientItem>();
                    string clientSql = "SELECT Id, FullName FROM Clients ORDER BY FullName";
                    using (var cmd = new NpgsqlCommand(clientSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            clients.Add(new ClientItem { Id = reader.GetInt32(0), FullName = reader.GetString(1) });
                        }
                    }
                    ClientSelect.ItemsSource = clients;
                    ClientSelect.DisplayMemberPath = "FullName";
                    ClientSelect.SelectedValuePath = "Id";

                    UpdateStatus($"Загружено: категорий {categories.Count}, товаров {products.Count}, клиентов {clients.Count}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();

                    string revenueSql = @"SELECT COALESCE(SUM(p.Price * s.Quantity), 0) 
                                          FROM Sales s
                                          JOIN Products p ON s.ProductId = p.Id";
                    if (currentFilterStart.HasValue && currentFilterEnd.HasValue)
                    {
                        revenueSql += $" WHERE s.SaleDate BETWEEN '{currentFilterStart.Value:yyyy-MM-dd}' AND '{currentFilterEnd.Value:yyyy-MM-dd}'";
                    }
                    using (var cmd = new NpgsqlCommand(revenueSql, conn))
                    {
                        decimal revenue = Convert.ToDecimal(cmd.ExecuteScalar());
                        TotalRevenueText.Text = $"{revenue:N0} ₽";
                    }

                    string avgSql = @"SELECT COALESCE(AVG(p.Price * s.Quantity), 0) 
                                      FROM Sales s
                                      JOIN Products p ON s.ProductId = p.Id";
                    if (currentFilterStart.HasValue && currentFilterEnd.HasValue)
                    {
                        avgSql += $" WHERE s.SaleDate BETWEEN '{currentFilterStart.Value:yyyy-MM-dd}' AND '{currentFilterEnd.Value:yyyy-MM-dd}'";
                    }
                    using (var cmd = new NpgsqlCommand(avgSql, conn))
                    {
                        decimal avgCheck = Convert.ToDecimal(cmd.ExecuteScalar());
                        AverageCheckText.Text = $"{avgCheck:N0} ₽";
                    }

                    string countSql = "SELECT COUNT(*) FROM Sales";
                    if (currentFilterStart.HasValue && currentFilterEnd.HasValue)
                    {
                        countSql += $" WHERE SaleDate BETWEEN '{currentFilterStart.Value:yyyy-MM-dd}' AND '{currentFilterEnd.Value:yyyy-MM-dd}'";
                    }
                    using (var cmd = new NpgsqlCommand(countSql, conn))
                    {
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        TotalSalesCountText.Text = count.ToString();
                    }

                    string topSql = @"SELECT p.Name, SUM(s.Quantity) as TotalQty
                                      FROM Sales s
                                      JOIN Products p ON s.ProductId = p.Id";
                    if (currentFilterStart.HasValue && currentFilterEnd.HasValue)
                    {
                        topSql += $" WHERE s.SaleDate BETWEEN '{currentFilterStart.Value:yyyy-MM-dd}' AND '{currentFilterEnd.Value:yyyy-MM-dd}'";
                    }
                    topSql += " GROUP BY p.Name ORDER BY TotalQty DESC LIMIT 1";

                    using (var cmd = new NpgsqlCommand(topSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            TopProductText.Text = $"{reader.GetString(0)} ({reader.GetInt32(1)} шт.)";
                        }
                        else
                        {
                            TopProductText.Text = "Нет данных";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка статистики: {ex.Message}");
            }
        }

        private void AddProductBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewProductName.Text))
            {
                ShowError("Введите название товара!");
                return;
            }
            if (!decimal.TryParse(NewProductPrice.Text, out decimal price))
            {
                ShowError("Введите корректную цену!");
                return;
            }
            if (CategorySelect.SelectedValue == null)
            {
                ShowError("Выберите категорию!");
                return;
            }

            int categoryId = (int)CategorySelect.SelectedValue;

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Products (Name, CategoryId, Price) VALUES (@name, @cat, @price)";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", NewProductName.Text.Trim());
                        cmd.Parameters.AddWithValue("@cat", categoryId);
                        cmd.Parameters.AddWithValue("@price", price);
                        cmd.ExecuteNonQuery();
                    }
                }
                NewProductName.Text = "";
                NewProductPrice.Text = "";
                LoadProducts();
                LoadSelectors();
                UpdateStatus("Товар добавлен!");
                ShowMessage("Успех", "Товар успешно добавлен");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        private void RefreshProductsBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void AddSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ProductSelect.SelectedValue == null)
            {
                ShowError("Выберите товар!");
                return;
            }
            if (ClientSelect.SelectedValue == null)
            {
                ShowError("Выберите клиента!");
                return;
            }
            if (!int.TryParse(SaleQuantity.Text, out int quantity) || quantity <= 0)
            {
                ShowError("Введите корректное количество!");
                return;
            }

            DateTime date = SaleDate.SelectedDate ?? DateTime.Today;
            int productId = (int)ProductSelect.SelectedValue;
            int clientId = (int)ClientSelect.SelectedValue;

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Sales (ProductId, ClientId, Quantity, SaleDate) VALUES (@pid, @cid, @qty, @date)";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", productId);
                        cmd.Parameters.AddWithValue("@cid", clientId);
                        cmd.Parameters.AddWithValue("@qty", quantity);
                        cmd.Parameters.AddWithValue("@date", date);
                        cmd.ExecuteNonQuery();
                    }
                }
                SaleQuantity.Text = "1";
                SaleDate.SelectedDate = null;
                LoadSales();
                UpdateStatistics();
                UpdateStatus("Продажа добавлена!");
                ShowMessage("Успех", "Продажа успешно добавлена");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        private void ApplyFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FilterStartDate.SelectedDate.HasValue && FilterEndDate.SelectedDate.HasValue)
            {
                currentFilterStart = FilterStartDate.SelectedDate.Value;
                currentFilterEnd = FilterEndDate.SelectedDate.Value;
                LoadSales();
                UpdateStatistics();
                UpdateStatus($"Фильтр: {currentFilterStart:dd.MM.yyyy} - {currentFilterEnd:dd.MM.yyyy}");
            }
            else
            {
                ShowError("Выберите обе даты!");
            }
        }

        private void ResetFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            currentFilterStart = null;
            currentFilterEnd = null;
            FilterStartDate.SelectedDate = null;
            FilterEndDate.SelectedDate = null;
            LoadSales();
            UpdateStatistics();
            UpdateStatus("Фильтр сброшен");
        }

        private void GenerateReportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!ReportStartDate.SelectedDate.HasValue || !ReportEndDate.SelectedDate.HasValue)
            {
                ShowError("Выберите период для отчёта!");
                return;
            }

            DateTime start = ReportStartDate.SelectedDate.Value;
            DateTime end = ReportEndDate.SelectedDate.Value;

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"SELECT p.Name, SUM(s.Quantity) as Quantity, 
                                          SUM(p.Price * s.Quantity) as Revenue
                                   FROM Sales s
                                   JOIN Products p ON s.ProductId = p.Id
                                   WHERE s.SaleDate BETWEEN @start AND @end
                                   GROUP BY p.Name
                                   ORDER BY Revenue DESC";

                    DataTable dt = new DataTable();
                    using (var adapter = new NpgsqlDataAdapter(sql, conn))
                    {
                        adapter.SelectCommand.Parameters.AddWithValue("@start", start);
                        adapter.SelectCommand.Parameters.AddWithValue("@end", end);
                        adapter.Fill(dt);
                    }

                    decimal totalRevenue = 0;
                    int totalQuantity = 0;
                    string topProduct = "Нет данных";
                    int topQty = 0;

                    string report = $"📄 ОТЧЁТ ЗА ПЕРИОД: {start:dd.MM.yyyy} - {end:dd.MM.yyyy}\n";
                    report += new string('=', 55) + "\n\n";

                    foreach (DataRow row in dt.Rows)
                    {
                        string name = row["Name"].ToString();
                        int qty = Convert.ToInt32(row["Quantity"]);
                        decimal revenue = Convert.ToDecimal(row["Revenue"]);

                        report += $"• {name}:\n";
                        report += $"   Продано: {qty} шт.\n";
                        report += $"   Выручка: {revenue:N0} ₽\n\n";

                        totalRevenue += revenue;
                        totalQuantity += qty;

                        if (qty > topQty)
                        {
                            topQty = qty;
                            topProduct = name;
                        }
                    }

                    decimal avgCheck = totalQuantity > 0 ? totalRevenue / totalQuantity : 0;

                    report += new string('=', 55) + "\n";
                    report += $"📊 ИТОГО:\n";
                    report += $"   Всего продаж: {totalQuantity} шт.\n";
                    report += $"   Общая выручка: {totalRevenue:N0} ₽\n";
                    report += $"   Средний чек: {avgCheck:N0} ₽\n";
                    report += $"   Самый продаваемый товар: {topProduct} ({topQty} шт.)\n";

                    ReportText.Text = report;

                    string saveSql = @"INSERT INTO Reports (StartDate, EndDate, TotalRevenue, TotalSales, AverageCheck, TopProduct) 
                                       VALUES (@start, @end, @rev, @sales, @avg, @top)";
                    using (var cmd = new NpgsqlCommand(saveSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@start", start);
                        cmd.Parameters.AddWithValue("@end", end);
                        cmd.Parameters.AddWithValue("@rev", totalRevenue);
                        cmd.Parameters.AddWithValue("@sales", totalQuantity);
                        cmd.Parameters.AddWithValue("@avg", avgCheck);
                        cmd.Parameters.AddWithValue("@top", $"{topProduct} ({topQty} шт.)");
                        cmd.ExecuteNonQuery();
                    }

                    UpdateStatus("Отчёт сформирован и сохранён в БД");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка формирования отчёта: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus($"Ошибка: {message}");
        }

        private void ShowMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
        }
    }

    public class CategoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ProductItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ClientItem
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }
}