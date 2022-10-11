using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Flowtics {
    public partial class Form2 : Form {
        Form1 f1;
        public Form2(string d_n,string item_mac,string ip_addr, Form1 f1_) {
            InitializeComponent();
            this.Text = "Flowtics: Device Details";
            f1 = f1_;

            guna2TextBox1.Text = d_n;
            guna2TextBox2.Text = ip_addr;
            guna2TextBox5.Text = item_mac;
            guna2TextBox3.Text = "ROUTER";
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(guna2Button4,"Block device from network");
        }

        private void Form2_Load(object sender, EventArgs e) {

        }

        private void guna2Panel1_Paint(object sender, PaintEventArgs e) {

        }

        private void label1_Click(object sender, EventArgs e) {

        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e) {

        }

        private void guna2Button4_Click(object sender, EventArgs e) {
            string device_n = guna2TextBox1.Text;
            DialogResult verify = MessageBox.Show("Block `" + device_n + "` device from your network?\nYou can always unblock this device later","Flowtics System",
                MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
            if(verify == DialogResult.Yes) {
                this.Close();
            }
        }

        private void label2_Click(object sender, EventArgs e) {

        }

        private void guna2TextBox2_TextChanged(object sender, EventArgs e) {

        }

        private void guna2TextBox3_TextChanged(object sender, EventArgs e) {

        }

        private void guna2TextBox5_TextChanged(object sender, EventArgs e) {

        }

        private void guna2TextBox4_TextChanged(object sender, EventArgs e) {

        }

        private void label1_Click_1(object sender, EventArgs e) {

        }

        private void label3_Click(object sender, EventArgs e) {

        }
    }
}
