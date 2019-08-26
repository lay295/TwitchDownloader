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
            this.label2.Location = new System.Drawing.Point(110, 123);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(98, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Background Color :";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(356, 123);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(44, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Height :";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(359, 150);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(41, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Width :";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(129, 150);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(79, 13);
            this.label7.TabIndex = 3;
            this.label7.Text = "BTTV Emotes :";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(249, 150);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(70, 13);
            this.label8.TabIndex = 3;
            this.label8.Text = "FFZ Emotes :";
            // 
            // btnColor
            // 
            this.btnColor.BackColor = System.Drawing.Color.Black;
            this.btnColor.Location = new System.Drawing.Point(212, 119);
            this.btnColor.Name = "btnColor";
            this.btnColor.Size = new System.Drawing.Size(31, 20);
            this.btnColor.TabIndex = 4;
            this.btnColor.UseVisualStyleBackColor = false;
            this.btnColor.Click += new System.EventHandler(this.BntColor_Click);
            // 
            // textColor
            // 
            this.textColor.Location = new System.Drawing.Point(249, 119);
            this.textColor.Name = "textColor";
            this.textColor.Size = new System.Drawing.Size(100, 20);
            this.textColor.TabIndex = 5;
            this.textColor.Text = "#111111";
            this.textColor.TextChanged += new System.EventHandler(this.TextColor_TextChanged);
            // 
            // textHeight
            // 
            this.textHeight.Location = new System.Drawing.Point(406, 120);
            this.textHeight.Name = "textHeight";
            this.textHeight.Size = new System.Drawing.Size(70, 20);
            this.textHeight.TabIndex = 7;
            this.textHeight.Text = "500";
            // 
            // textWidth
            // 
            this.textWidth.Location = new System.Drawing.Point(406, 147);
            this.textWidth.Name = "textWidth";
            this.textWidth.Size = new System.Drawing.Size(70, 20);
            this.textWidth.TabIndex = 7;
            this.textWidth.Text = "300";
            // 
            // checkBTTV
            // 
            this.checkBTTV.AutoSize = true;
            this.checkBTTV.Checked = true;
            this.checkBTTV.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBTTV.Location = new System.Drawing.Point(214, 150);
            this.checkBTTV.Name = "checkBTTV";
            this.checkBTTV.Size = new System.Drawing.Size(15, 14);
            this.checkBTTV.TabIndex = 8;
            this.checkBTTV.UseVisualStyleBackColor = true;
            // 
            // checkFFZ
            // 
            this.checkFFZ.AutoSize = true;
            this.checkFFZ.Checked = true;
            this.checkFFZ.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkFFZ.Location = new System.Drawing.Point(325, 150);
            this.checkFFZ.Name = "checkFFZ";
            this.checkFFZ.Size = new System.Drawing.Size(15, 14);
            this.checkFFZ.TabIndex = 8;
            this.checkFFZ.UseVisualStyleBackColor = true;
            // 
            // textJSON
            // 
            this.textJSON.Location = new System.Drawing.Point(190, 45);
            this.textJSON.Name = "textJSON";
            this.textJSON.Size = new System.Drawing.Size(221, 20);
            this.textJSON.TabIndex = 0;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(417, 43);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(75, 23);
            this.btnBrowse.TabIndex = 10;
            this.btnBrowse.Text = "Browse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(124, 48);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(60, 13);
            this.label9.TabIndex = 11;
            this.label9.Text = "JSON File :";
            // 
            // btnRender
            // 
            this.btnRender.Location = new System.Drawing.Point(274, 274);
            this.btnRender.Name = "btnRender";
            this.btnRender.Size = new System.Drawing.Size(111, 41);
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
            this.statusStrip.Location = new System.Drawing.Point(0, 368);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(812, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 67;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStatus
            // 
            this.toolStatus.Name = "toolStatus";
            this.toolStatus.Size = new System.Drawing.Size(26, 17);
            this.toolStatus.Text = "Idle";
            // 
            // toolProgressBar
            // 
            this.toolProgressBar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolProgressBar.Name = "toolProgressBar";
            this.toolProgressBar.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.toolProgressBar.Size = new System.Drawing.Size(200, 16);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(606, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(31, 13);
            this.label6.TabIndex = 69;
            this.label6.Text = "Log :";
            // 
            // textLog
            // 
            this.textLog.Location = new System.Drawing.Point(609, 78);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.Size = new System.Drawing.Size(175, 264);
            this.textLog.TabIndex = 68;
            // 
            // frmChatRender
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(812, 390);
            this.ControlBox = false;
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
            this.Name = "frmChatRender";
            this.Text = "frmChatRender";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
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
    }
}