namespace TwitchDownloader
{
    partial class frmChatRender
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.colorDialog = new System.Windows.Forms.ColorDialog();
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.btnColor = new System.Windows.Forms.Button();
            this.textColor = new System.Windows.Forms.TextBox();
            this.textHeight = new System.Windows.Forms.TextBox();
            this.textWidth = new System.Windows.Forms.TextBox();
            this.checkBTTV = new System.Windows.Forms.CheckBox();
            this.checkFFZ = new System.Windows.Forms.CheckBox();
            this.textJSON = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.label9 = new System.Windows.Forms.Label();
            this.btnRender = new System.Windows.Forms.Button();
            this.backgroundRenderManager = new System.ComponentModel.BackgroundWorker();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.label6 = new System.Windows.Forms.Label();
            this.textLog = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.checkOutline = new System.Windows.Forms.CheckBox();
            this.comboFonts = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.textFontSize = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.textUpdateTime = new System.Windows.Forms.TextBox();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // colorDialog
            // 
            this.colorDialog.Color = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(17)))), ((int)(((byte)(17)))));
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(147, 151);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(129, 17);
            this.label2.TabIndex = 2;
            this.label2.Text = "Background Color :";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(475, 151);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 17);
            this.label4.TabIndex = 2;
            this.label4.Text = "Height :";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(479, 185);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(52, 17);
            this.label5.TabIndex = 2;
            this.label5.Text = "Width :";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(172, 185);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(103, 17);
            this.label7.TabIndex = 3;
            this.label7.Text = "BTTV Emotes :";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(332, 185);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(92, 17);
            this.label8.TabIndex = 3;
            this.label8.Text = "FFZ Emotes :";
            // 
            // btnColor
            // 
            this.btnColor.BackColor = System.Drawing.Color.Black;
            this.btnColor.Location = new System.Drawing.Point(283, 146);
            this.btnColor.Margin = new System.Windows.Forms.Padding(4);
            this.btnColor.Name = "btnColor";
            this.btnColor.Size = new System.Drawing.Size(41, 25);
            this.btnColor.TabIndex = 4;
            this.btnColor.UseVisualStyleBackColor = false;
            this.btnColor.Click += new System.EventHandler(this.BntColor_Click);
            // 
            // textColor
            // 
            this.textColor.Location = new System.Drawing.Point(332, 148);
            this.textColor.Margin = new System.Windows.Forms.Padding(4);
            this.textColor.Name = "textColor";
            this.textColor.Size = new System.Drawing.Size(132, 22);
            this.textColor.TabIndex = 5;
            this.textColor.Text = "#111111";
            this.textColor.TextChanged += new System.EventHandler(this.TextColor_TextChanged);
            // 
            // textHeight
            // 
            this.textHeight.Location = new System.Drawing.Point(541, 148);
            this.textHeight.Margin = new System.Windows.Forms.Padding(4);
            this.textHeight.Name = "textHeight";
            this.textHeight.Size = new System.Drawing.Size(92, 22);
            this.textHeight.TabIndex = 7;
            this.textHeight.Text = "500";
            // 
            // textWidth
            // 
            this.textWidth.Location = new System.Drawing.Point(541, 181);
            this.textWidth.Margin = new System.Windows.Forms.Padding(4);
            this.textWidth.Name = "textWidth";
            this.textWidth.Size = new System.Drawing.Size(92, 22);
            this.textWidth.TabIndex = 7;
            this.textWidth.Text = "300";
            // 
            // checkBTTV
            // 
            this.checkBTTV.AutoSize = true;
            this.checkBTTV.Checked = true;
            this.checkBTTV.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBTTV.Location = new System.Drawing.Point(285, 185);
            this.checkBTTV.Margin = new System.Windows.Forms.Padding(4);
            this.checkBTTV.Name = "checkBTTV";
            this.checkBTTV.Size = new System.Drawing.Size(18, 17);
            this.checkBTTV.TabIndex = 8;
            this.checkBTTV.UseVisualStyleBackColor = true;
            // 
            // checkFFZ
            // 
            this.checkFFZ.AutoSize = true;
            this.checkFFZ.Checked = true;
            this.checkFFZ.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkFFZ.Location = new System.Drawing.Point(433, 185);
            this.checkFFZ.Margin = new System.Windows.Forms.Padding(4);
            this.checkFFZ.Name = "checkFFZ";
            this.checkFFZ.Size = new System.Drawing.Size(18, 17);
            this.checkFFZ.TabIndex = 8;
            this.checkFFZ.UseVisualStyleBackColor = true;
            // 
            // textJSON
            // 
            this.textJSON.Location = new System.Drawing.Point(253, 55);
            this.textJSON.Margin = new System.Windows.Forms.Padding(4);
            this.textJSON.Name = "textJSON";
            this.textJSON.Size = new System.Drawing.Size(293, 22);
            this.textJSON.TabIndex = 0;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(556, 53);
            this.btnBrowse.Margin = new System.Windows.Forms.Padding(4);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(100, 28);
            this.btnBrowse.TabIndex = 10;
            this.btnBrowse.Text = "Browse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(165, 59);
            this.label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(79, 17);
            this.label9.TabIndex = 11;
            this.label9.Text = "JSON File :";
            // 
            // btnRender
            // 
            this.btnRender.Location = new System.Drawing.Point(365, 337);
            this.btnRender.Margin = new System.Windows.Forms.Padding(4);
            this.btnRender.Name = "btnRender";
            this.btnRender.Size = new System.Drawing.Size(148, 50);
            this.btnRender.TabIndex = 49;
            this.btnRender.Text = "Render Chat";
            this.btnRender.UseVisualStyleBackColor = true;
            this.btnRender.Click += new System.EventHandler(this.BtnRender_Click);
            // 
            // backgroundRenderManager
            // 
            this.backgroundRenderManager.WorkerReportsProgress = true;
            this.backgroundRenderManager.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackgroundRenderManager_DoWork);
            this.backgroundRenderManager.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BackgroundRenderManager_ProgressChanged);
            this.backgroundRenderManager.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundRenderManager_RunWorkerCompleted);
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStatus,
            this.toolProgressBar});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 454);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.statusStrip.Size = new System.Drawing.Size(1083, 26);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 67;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStatus
            // 
            this.toolStatus.Name = "toolStatus";
            this.toolStatus.Size = new System.Drawing.Size(34, 20);
            this.toolStatus.Text = "Idle";
            // 
            // toolProgressBar
            // 
            this.toolProgressBar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolProgressBar.Name = "toolProgressBar";
            this.toolProgressBar.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.toolProgressBar.Size = new System.Drawing.Size(267, 18);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(745, 33);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(40, 17);
            this.label6.TabIndex = 69;
            this.label6.Text = "Log :";
            // 
            // textLog
            // 
            this.textLog.Location = new System.Drawing.Point(748, 59);
            this.textLog.Margin = new System.Windows.Forms.Padding(4);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.Size = new System.Drawing.Size(296, 361);
            this.textLog.TabIndex = 68;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(84, 218);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(191, 17);
            this.label1.TabIndex = 70;
            this.label1.Text = "Username/Message Outline :";
            // 
            // checkOutline
            // 
            this.checkOutline.AutoSize = true;
            this.checkOutline.Location = new System.Drawing.Point(285, 218);
            this.checkOutline.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkOutline.Name = "checkOutline";
            this.checkOutline.Size = new System.Drawing.Size(18, 17);
            this.checkOutline.TabIndex = 71;
            this.checkOutline.UseVisualStyleBackColor = true;
            // 
            // comboFonts
            // 
            this.comboFonts.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFonts.FormattingEnabled = true;
            this.comboFonts.Location = new System.Drawing.Point(385, 213);
            this.comboFonts.Margin = new System.Windows.Forms.Padding(4);
            this.comboFonts.Name = "comboFonts";
            this.comboFonts.Size = new System.Drawing.Size(248, 24);
            this.comboFonts.TabIndex = 73;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(332, 217);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(44, 17);
            this.label3.TabIndex = 72;
            this.label3.Text = "Font :";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(332, 255);
            this.label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(75, 17);
            this.label10.TabIndex = 74;
            this.label10.Text = "Font Size :";
            // 
            // textFontSize
            // 
            this.textFontSize.Location = new System.Drawing.Point(415, 251);
            this.textFontSize.Margin = new System.Windows.Forms.Padding(4);
            this.textFontSize.Name = "textFontSize";
            this.textFontSize.Size = new System.Drawing.Size(49, 22);
            this.textFontSize.TabIndex = 75;
            this.textFontSize.Text = "9";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(481, 255);
            this.label11.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(93, 17);
            this.label11.TabIndex = 76;
            this.label11.Text = "Update Time:";
            // 
            // textUpdateTime
            // 
            this.textUpdateTime.Location = new System.Drawing.Point(584, 251);
            this.textUpdateTime.Margin = new System.Windows.Forms.Padding(4);
            this.textUpdateTime.Name = "textUpdateTime";
            this.textUpdateTime.Size = new System.Drawing.Size(49, 22);
            this.textUpdateTime.TabIndex = 77;
            this.textUpdateTime.Text = "1.0";
            // 
            // frmChatRender
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1083, 480);
            this.ControlBox = false;
            this.Controls.Add(this.textUpdateTime);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.textFontSize);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.comboFonts);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.checkOutline);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textLog);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.btnRender);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.textJSON);
            this.Controls.Add(this.checkFFZ);
            this.Controls.Add(this.checkBTTV);
            this.Controls.Add(this.textWidth);
            this.Controls.Add(this.textHeight);
            this.Controls.Add(this.textColor);
            this.Controls.Add(this.btnColor);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "frmChatRender";
            this.Text = "frmChatRender";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.FrmChatRender_Load);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ColorDialog colorDialog;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button btnColor;
        private System.Windows.Forms.TextBox textColor;
        private System.Windows.Forms.TextBox textHeight;
        private System.Windows.Forms.TextBox textWidth;
        private System.Windows.Forms.CheckBox checkBTTV;
        private System.Windows.Forms.CheckBox checkFFZ;
        private System.Windows.Forms.TextBox textJSON;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button btnRender;
        private System.ComponentModel.BackgroundWorker backgroundRenderManager;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStatus;
        private System.Windows.Forms.ToolStripProgressBar toolProgressBar;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textLog;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkOutline;
        private System.Windows.Forms.ComboBox comboFonts;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox textFontSize;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox textUpdateTime;
    }
}