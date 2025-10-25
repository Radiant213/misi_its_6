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
    public partial class FormKategori : Form
    {

        public FormKategori()
        {
            InitializeComponent();
        }


        private void LoadDataKategori()
        {
            dgvKategori.Rows.Clear();
            dgvKategori.Columns.Clear();
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                string query = "SELECT Id, NamaKategori FROM Kategori";
                SqlCommand cmd = new SqlCommand(query, conn);
                SqlDataReader reader = cmd.ExecuteReader();
                dgvKategori.Columns.Add("Id", "ID");
                dgvKategori.Columns.Add("NamaKategori", "Nama Kategori");
                while (reader.Read())
                {
                    dgvKategori.Rows.Add(reader["Id"], reader["NamaKategori"]);
                }
                reader.Close();
            }
        }
        private void FormKategori_Load(object sender, EventArgs e)
        {
            LoadDataKategori();
            
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void btnTambah_Click(object sender, EventArgs e)
        {
            string nama = txtNamaKategori.Text.Trim();

            if (string.IsNullOrWhiteSpace(nama))
            {
                MessageBox.Show("Nama kategori tidak boleh kosong.");
                return;
            }

            if (nama.Length < 3)
            {
                MessageBox.Show("Nama kategori minimal 3 karakter.");
                return;
            }

            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                string query = "INSERT INTO Kategori (NamaKategori) VALUES (@nama)";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@nama", nama);
                cmd.ExecuteNonQuery();
                MessageBox.Show("Kategori berhasil ditambahkan!");
                txtNamaKategori.Clear();
                LoadDataKategori();
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (dgvKategori.SelectedRows.Count == 0)
            {
                MessageBox.Show("Pilih kategori terlebih dahulu.");
                return;
            }
            int id = Convert.ToInt32(dgvKategori.SelectedRows[0].Cells["Id"].Value);
            string nama = txtNamaKategori.Text.Trim();
            if (string.IsNullOrWhiteSpace(nama))
            {
                MessageBox.Show("Nama kategori tidak boleh kosong.");
                return;
            }

            if (nama.Length < 3)
            {
                MessageBox.Show("Nama kategori minimal 3 karakter.");
                return;
            }

            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                string query = "UPDATE Kategori SET NamaKategori = @nama WHERE Id = @id";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@nama", nama);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                MessageBox.Show("Kategori berhasil diubah.");
                txtNamaKategori.Clear();
                LoadDataKategori();
            }
        }

        private void btnHapus_Click(object sender, EventArgs e)
        {
            if (dgvKategori.SelectedRows.Count == 0)
            {
                MessageBox.Show("Pilih kategori yang ingin dihapus.");
                return;
            }
            int id = Convert.ToInt32(dgvKategori.SelectedRows[0].Cells["Id"].Value);
            DialogResult confirm = MessageBox.Show(
            "Yakin ingin menghapus kategori ini?",
            "Konfirmasi",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
            );
            if (confirm == DialogResult.Yes)
            {
                using (SqlConnection conn = Koneksi.GetConnection())
                {
                    conn.Open();
                    string query = "DELETE FROM Kategori WHERE Id = @id";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                MessageBox.Show("Kategori berhasil dihapus.");
                LoadDataKategori();
            }
        }

        private void dgvKategori_SelectionChanged(object sender, EventArgs e)
        {
            bool rowSelected = dgvKategori.SelectedRows.Count > 0;
            btnEdit.Enabled = rowSelected;
            btnHapus.Enabled = rowSelected;

            if (dgvKategori.SelectedRows.Count == 0) 
            {
                txtJumlahProduk.Text = "0";
                return;
            }

            int kategoriId =
            Convert.ToInt32(dgvKategori.SelectedRows[0].Cells["Id"].Value);
            txtNamaKategori.Text =
            dgvKategori.SelectedRows[0].Cells["NamaKategori"].Value.ToString();
            dgvProdukTerkait.Rows.Clear();
            dgvProdukTerkait.Columns.Clear();
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();

                // hitung jumlah produk untuk kategori terpilih
                string countQuery = "SELECT COUNT(*) FROM Produk WHERE KategoriId = @kategoriId";
                using (SqlCommand cmdCount = new SqlCommand(countQuery, conn))
                {
                    cmdCount.Parameters.AddWithValue("@kategoriId", kategoriId);
                    object result = cmdCount.ExecuteScalar();
                    int jumlah = (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                    txtJumlahProduk.Text = jumlah.ToString();
                }

                string query = "SELECT NamaProduk, Harga, Stok, Deskripsi FROM Produk WHERE KategoriId = @kategoriId";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@kategoriId", kategoriId);
                SqlDataReader reader = cmd.ExecuteReader();
                dgvProdukTerkait.Columns.Add("NamaProduk", "Nama Produk");
                dgvProdukTerkait.Columns.Add("Harga", "Harga");
                dgvProdukTerkait.Columns.Add("Stok", "Stok");
                dgvProdukTerkait.Columns.Add("Deskripsi", "Deskripsi");
                while (reader.Read())
                {
                    string hargaRupiah = "Rp." + string.Format("{0:N0}", reader["Harga"]);
                    dgvProdukTerkait.Rows.Add(
                    reader["NamaProduk"],
                    hargaRupiah,
                    reader["Stok"],
                    reader["Deskripsi"]
                    );
                }
                reader.Close();
            }
        }

        private void txtJumlahProduk_Click(object sender, EventArgs e)
        {

        }
    }
}
