﻿using System;
using System.Windows.Forms;

namespace BlockTree.UI
{
    internal partial class CreateWalletDialog : Form
    {
        public CreateWalletDialog()
        {
            InitializeComponent();
        }

        public string Password
        {
            get
            {
                return textBox2.Text;
            }
            set
            {
                textBox2.Text = value;
                textBox3.Text = value;
            }
        }

        public string WalletPath
        {
            get
            {
                return textBox1.Text;
            }
            set
            {
                textBox1.Text = value;
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.TextLength == 0 || textBox2.TextLength == 0 || textBox3.TextLength == 0)
            {
                button2.Enabled = false;
                return;
            }
            if (textBox2.Text != textBox3.Text)
            {
                button2.Enabled = false;
                return;
            }
            button2.Enabled = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = saveFileDialog1.FileName;
            }
        }
    }
}
