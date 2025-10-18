using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FormProduk
{
    public partial class FormTransaksiPenjualan : Form
    {
        public FormTransaksiPenjualan()
        {
            InitializeComponent();
        }
        private void HitungTotal()
        {
            decimal total = 0;
            foreach (DataGridViewRow row in dgvItem.Rows)
            {
                if (row.IsNewRow) continue;
                total += Convert.ToDecimal(row.Cells["Subtotal"].Value);
            }
            lblTotal.Text = $"Total: Rp {total:N0}";
        }

        private decimal GetHargaProduk(int produkId)
        {
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT Harga FROM Produk WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@id", produkId);
                return (decimal)cmd.ExecuteScalar();
            }
        }

        private int GetStokProduk(int produkId)
        {
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT Stok FROM Produk WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@id", produkId);
                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt32(result);
            }
        }

        private void btnTambah_Click(object sender, EventArgs e)
        {
            if (cmbProduk.SelectedItem == null || !int.TryParse(txtJumlah.Text, out int jumlah) || jumlah <= 0)
            {
                MessageBox.Show("Pilih produk dan jumlah valid.");
                return;
            }
            var selected = (KeyValuePair<int, string>)cmbProduk.SelectedItem;
            int produkId = selected.Key;
            string namaProduk = selected.Value;

            // Cek stok saat menambah
            int stok = GetStokProduk(produkId);
            if (jumlah > stok)
            {
                MessageBox.Show($"Stok tidak cukup. Stok saat ini: {stok}");
                return;
            }

            decimal harga = GetHargaProduk(produkId);
            decimal subtotal = harga * jumlah;
            // Pastikan nilai yang dimasukkan tetap numeric (untuk keperluan database), formatting hanya untuk tampilan
            dgvItem.Rows.Add(produkId, namaProduk, harga, jumlah, subtotal);
            HitungTotal();
            UpdateHapusButtonState();
        }

        private void btnSimpan_Click(object sender, EventArgs e)
        {
            if (dgvItem.Rows.Count == 0 || dgvItem.Rows.Cast<DataGridViewRow>().All(r => r.IsNewRow))
            {
                MessageBox.Show("Belum ada item ditambahkan.");
                return;
            }
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlTransaction trx = conn.BeginTransaction();
                try
                {
                    // 1. Insert ke Penjualan
                    decimal total = dgvItem.Rows.Cast<DataGridViewRow>()
                        .Where(r => !r.IsNewRow)
                        .Sum(r => Convert.ToDecimal(r.Cells["Subtotal"].Value));
                    SqlCommand cmdPenjualan = new SqlCommand(
                        "INSERT INTO Penjualan (Tanggal, TotalHarga) VALUES (@tgl, @total); SELECT SCOPE_IDENTITY();", conn, trx);
                    cmdPenjualan.Parameters.AddWithValue("@tgl", DateTime.Now);
                    cmdPenjualan.Parameters.AddWithValue("@total", total);
                    int penjualanId = Convert.ToInt32(cmdPenjualan.ExecuteScalar());

                    // 2. Untuk setiap item: cek & kurangi stok lalu insert PenjualanDetail
                    foreach (DataGridViewRow row in dgvItem.Rows)
                    {
                        if (row.IsNewRow) continue;

                        int prodId = Convert.ToInt32(row.Cells["ProdukId"].Value);
                        int jumlah = Convert.ToInt32(row.Cells["Jumlah"].Value);
                        decimal subtotal = Convert.ToDecimal(row.Cells["Subtotal"].Value);

                        // Update stok secara aman: hanya update bila stok cukup
                        SqlCommand cmdUpdateStok = new SqlCommand(
                            "UPDATE Produk SET Stok = Stok - @jumlah WHERE Id = @id AND Stok >= @jumlah", conn, trx);
                        cmdUpdateStok.Parameters.AddWithValue("@jumlah", jumlah);
                        cmdUpdateStok.Parameters.AddWithValue("@id", prodId);
                        int affected = cmdUpdateStok.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            // Stok tidak cukup atau produk tidak ditemukan -> rollback
                            throw new Exception($"Stok tidak cukup untuk produk '{row.Cells["NamaProduk"].Value}' (Id {prodId}).");
                        }

                        // Insert detail penjualan
                        SqlCommand cmdDetail = new SqlCommand(
                            @"INSERT INTO PenjualanDetail (PenjualanId, ProdukId, Jumlah, Subtotal) VALUES (@pjId, @prodId, @jumlah, @subtotal)", conn, trx);
                        cmdDetail.Parameters.AddWithValue("@pjId", penjualanId);
                        cmdDetail.Parameters.AddWithValue("@prodId", prodId);
                        cmdDetail.Parameters.AddWithValue("@jumlah", jumlah);
                        cmdDetail.Parameters.AddWithValue("@subtotal", subtotal);
                        cmdDetail.ExecuteNonQuery();
                    }

                    trx.Commit();
                    MessageBox.Show("Transaksi berhasil disimpan!");
                    dgvItem.Rows.Clear();
                    HitungTotal();
                    UpdateHapusButtonState();
                }
                catch (Exception ex)
                {
                    try { trx.Rollback(); } catch { /* ignore rollback error */ }
                    MessageBox.Show("Gagal menyimpan transaksi: " + ex.Message);
                }
            }
        }

        private void FormTransaksiPenjualan_Load(object sender, EventArgs e)
        {
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT Id, NamaProduk FROM Produk", conn);
                SqlDataReader reader = cmd.ExecuteReader();
                Dictionary<int, string> produkDict = new Dictionary<int, string>();
                while (reader.Read())
                {
                    produkDict.Add((int)reader["Id"], reader["NamaProduk"].ToString());
                }
                cmbProduk.DataSource = new BindingSource(produkDict, null);
                cmbProduk.DisplayMember = "Value";
                cmbProduk.ValueMember = "Key";
            }

            // Setup kolom dgvItem (pastikan ValueType numeric supaya operasi sum aman)
            dgvItem.Columns.Add("ProdukId", "ProdukId");
            dgvItem.Columns["ProdukId"].Visible = false;

            var colNama = dgvItem.Columns.Add("NamaProduk", "Nama Produk");
            dgvItem.Columns["NamaProduk"].ValueType = typeof(string);

            dgvItem.Columns.Add("Harga", "Harga");
            dgvItem.Columns["Harga"].ValueType = typeof(decimal);

            dgvItem.Columns.Add("Jumlah", "Jumlah");
            dgvItem.Columns["Jumlah"].ValueType = typeof(int);

            dgvItem.Columns.Add("Subtotal", "Subtotal");
            dgvItem.Columns["Subtotal"].ValueType = typeof(decimal);

            // Subscribe ke event CellFormatting untuk menampilkan "Rp 1.000" tanpa mengubah nilai underlying
            dgvItem.CellFormatting += dgvItem_CellFormatting;

            // Subscribe ke SelectionChanged untuk enable/disable btnHapus
            dgvItem.SelectionChanged += dgvItem_SelectionChanged;
            dgvItem.ClearSelection();
            UpdateHapusButtonState();
        }

        private void dgvItem_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value == null) return;
            var colName = dgvItem.Columns[e.ColumnIndex].Name;
            if (colName == "Harga" || colName == "Subtotal")
            {
                try
                {
                    decimal val = Convert.ToDecimal(e.Value);
                    e.Value = $"Rp {val:N0}";
                    e.FormattingApplied = true;
                }
                catch
                {
                    
                }
            }
        }

        private void btnHapus_Click(object sender, EventArgs e)
        {
            // Hapus baris yang dipilih — jika user memilih cell, hapus row terkait
            var selectedRows = dgvItem.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .ToList();

            // Jika tidak ada selectedRows tapi ada selectedCells, ambil rows dari selectedCells
            if (selectedRows.Count == 0 && dgvItem.SelectedCells.Count > 0)
            {
                selectedRows = dgvItem.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(c => c.OwningRow)
                    .Where(r => r != null && !r.IsNewRow)
                    .Distinct()
                    .ToList();
            }

            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Pilih item yang ingin dihapus.");
                return;
            }

            // Konfirmasi (opsional) — hapus langsung sesuai permintaan
            if (MessageBox.Show($"Hapus {selectedRows.Count} item terpilih?", "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            foreach (var row in selectedRows)
            {
                dgvItem.Rows.Remove(row);
            }

            HitungTotal();
            UpdateHapusButtonState();
        }

        private void dgvItem_SelectionChanged(object sender, EventArgs e)
        {
            UpdateHapusButtonState();
        }

        private void UpdateHapusButtonState()
        {
            // Cek apakah ada baris valid yang terseleksi (SelectedRows atau SelectedCells)
            bool hasValidSelection = dgvItem.SelectedRows.Cast<DataGridViewRow>().Any(r => !r.IsNewRow);
            if (!hasValidSelection && dgvItem.SelectedCells.Count > 0)
            {
                hasValidSelection = dgvItem.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(c => c.OwningRow)
                    .Any(r => r != null && !r.IsNewRow);
            }

            if (btnHapus != null)
            {
                btnHapus.Enabled = hasValidSelection;
            }
        }
    }
}
