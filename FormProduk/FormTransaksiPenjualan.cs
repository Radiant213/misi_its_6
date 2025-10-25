using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
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

                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return 0; // ✅ aman dari NullReference
                return Convert.ToDecimal(result);
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
                if (result == null || result == DBNull.Value)
                    return 0;
                return Convert.ToInt32(result);
            }
        }

        private void btnTambah_Click(object sender, EventArgs e)
        {
            if (cmbProduk.SelectedItem == null)
            {
                MessageBox.Show("Pilih produk terlebih dahulu.");
                return;
            }

            int jumlah = (int)numJumlah.Value;
            if (jumlah <= 0)
            {
                MessageBox.Show("Jumlah harus lebih besar dari 0.");
                return;
            }

            if (cmbProduk.SelectedItem is not KeyValuePair<int, string> selected)
            {
                MessageBox.Show("Data produk tidak valid.");
                return;
            }

            int produkId = selected.Key;
            string namaProduk = selected.Value;

            int stok = GetStokProduk(produkId);
            if (jumlah > stok)
            {
                MessageBox.Show($"Stok tidak cukup. Stok saat ini: {stok}");
                return;
            }

            decimal harga = GetHargaProduk(produkId);
            decimal subtotal = harga * jumlah;

            dgvItem.Rows.Add(produkId, namaProduk, harga, jumlah, subtotal);
            HitungTotal();
        }

        private void btnSimpan_Click(object sender, EventArgs e)
        {
            if (dgvItem.Rows.Cast<DataGridViewRow>().All(r => r.IsNewRow))
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
                    decimal total = dgvItem.Rows.Cast<DataGridViewRow>()
                        .Where(r => !r.IsNewRow)
                        .Sum(r => Convert.ToDecimal(r.Cells["Subtotal"].Value));

                    SqlCommand cmdPenjualan = new SqlCommand(
                        "INSERT INTO Penjualan (Tanggal, TotalHarga) VALUES (@tgl, @total); SELECT SCOPE_IDENTITY();", conn, trx);
                    cmdPenjualan.Parameters.AddWithValue("@tgl", DateTime.Now);
                    cmdPenjualan.Parameters.AddWithValue("@total", total);
                    int penjualanId = Convert.ToInt32(cmdPenjualan.ExecuteScalar());

                    foreach (DataGridViewRow row in dgvItem.Rows)
                    {
                        if (row.IsNewRow) continue;

                        int prodId = Convert.ToInt32(row.Cells["ProdukId"].Value);
                        int jumlah = Convert.ToInt32(row.Cells["Jumlah"].Value);
                        decimal subtotal = Convert.ToDecimal(row.Cells["Subtotal"].Value);

                        SqlCommand cmdUpdateStok = new SqlCommand(
                            "UPDATE Produk SET Stok = Stok - @jumlah WHERE Id = @id AND Stok >= @jumlah", conn, trx);
                        cmdUpdateStok.Parameters.AddWithValue("@jumlah", jumlah);
                        cmdUpdateStok.Parameters.AddWithValue("@id", prodId);

                        int affected = cmdUpdateStok.ExecuteNonQuery();
                        if (affected == 0)
                            throw new Exception($"Stok tidak cukup untuk produk '{row.Cells["NamaProduk"].Value}' (Id {prodId}).");

                        SqlCommand cmdDetail = new SqlCommand(
                            @"INSERT INTO PenjualanDetail (PenjualanId, ProdukId, Jumlah, Subtotal) 
                              VALUES (@pjId, @prodId, @jumlah, @subtotal)", conn, trx);
                        cmdDetail.Parameters.AddWithValue("@pjId", penjualanId);
                        cmdDetail.Parameters.AddWithValue("@prodId", prodId);
                        cmdDetail.Parameters.AddWithValue("@jumlah", jumlah);
                        cmdDetail.Parameters.AddWithValue("@subtotal", subtotal);
                        cmdDetail.ExecuteNonQuery();
                    }

                    trx.Commit();
                    MessageBox.Show("Transaksi berhasil disimpan!");

                    dgvItem.Rows.Clear();
                    cmbProduk.SelectedIndex = -1; // ✅ reset UI
                    numJumlah.Value = 1;
                    HitungTotal();
                }
                catch (Exception ex)
                {
                    try { trx.Rollback(); } catch { }
                    MessageBox.Show("Gagal menyimpan transaksi: " + ex.Message);
                }
            }
        }

        private void FormTransaksiPenjualan_Load(object sender, EventArgs e)
        {
            try
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
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal memuat produk: " + ex.Message);
            }

            // ✅ Pastikan kolom hanya ditambah sekali
            if (dgvItem.Columns.Count == 0)
            {
                dgvItem.Columns.Add("ProdukId", "ProdukId");
                dgvItem.Columns["ProdukId"].Visible = false;
                dgvItem.Columns.Add("NamaProduk", "Nama Produk");
                dgvItem.Columns.Add("Harga", "Harga");
                dgvItem.Columns.Add("Jumlah", "Jumlah");
                dgvItem.Columns.Add("Subtotal", "Subtotal");

                DataGridViewButtonColumn btnHapus = new DataGridViewButtonColumn();
                btnHapus.HeaderText = "Aksi";
                btnHapus.Text = "Hapus";
                btnHapus.Name = "btnHapus";
                btnHapus.UseColumnTextForButtonValue = true;
                dgvItem.Columns.Add(btnHapus);
            }

            dgvItem.CellFormatting += dgvItem_CellFormatting;
        }

        private void dgvItem_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value == null) return;
            string colName = dgvItem.Columns[e.ColumnIndex].Name;
            if (colName == "Harga" || colName == "Subtotal")
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal val))
                {
                    e.Value = $"Rp {val:N0}";
                    e.FormattingApplied = true;
                }
            }
        }

        private void dgvItem_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 &&
                e.ColumnIndex == dgvItem.Columns["btnHapus"].Index &&
                e.RowIndex < dgvItem.Rows.Count)
            {
                var confirm = MessageBox.Show("Yakin mau hapus baris ini?", "Konfirmasi",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    dgvItem.Rows.RemoveAt(e.RowIndex);
                    HitungTotal();
                }
            }
        }
    }
}
