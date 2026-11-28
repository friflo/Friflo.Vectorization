namespace WinFormsApp;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing) {
        if (disposing && (components != null)) {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
        guiControl = new Friflo.SmGui.WinForms.SkiaGuiControl();
        SuspendLayout();
        // 
        // guiControl
        // 
        guiControl.Dock = System.Windows.Forms.DockStyle.Fill;
        guiControl.Location = new System.Drawing.Point(0, 0);
        guiControl.Name = "guiControl";
        guiControl.Size = new System.Drawing.Size(1111, 1055);
        guiControl.TabIndex = 0;
        guiControl.Text = "skiaGuiControl1";
        // 
        // Form1
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(15F, 37F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(1111, 1055);
        Controls.Add(guiControl);
        Text = "Form1";
        ResumeLayout(false);
    }

    private Friflo.SmGui.WinForms.SkiaGuiControl guiControl;

    #endregion
}