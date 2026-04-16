using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;

namespace FileCompare
{
    public partial class Form1 : Form
    {
        // map keys to chosen colors for matched files
        private readonly Dictionary<string, Color> matchColors = new();
        private readonly Color[] palette = new Color[] {
            Color.FromArgb(0x1f77b4), Color.FromArgb(0xff7f0e), Color.FromArgb(0x2ca02c),
            Color.FromArgb(0xd62728), Color.FromArgb(0x9467bd), Color.FromArgb(0x8c564b),
            Color.FromArgb(0xe377c2), Color.FromArgb(0x7f7f7f), Color.FromArgb(0xbcbd22),
            Color.FromArgb(0x17becf)
        };
        private void PopulateListView(ListView lv, string folderPath)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            try
            { // 폴더(디렉터리) 먼저 추가
                var dirs = Directory.EnumerateDirectories(folderPath)
                    .Select(p => new DirectoryInfo(p))
                    .OrderBy(d => d.Name);
                foreach (var d in dirs)
                {
                    var item = new ListViewItem(d.Name);
                    item.Tag = d; // keep DirectoryInfo for potential future use
                    item.SubItems.Add("<DIR>");
                    item.SubItems.Add(d.LastWriteTime.ToString("g"));
                    lv.Items.Add(item);
                }
                // 파일 추가
                var files = Directory.EnumerateFiles(folderPath)
                    .Select(p => new FileInfo(p))
                    .OrderBy(f => f.Name);
                foreach (var f in files)
                {
                    var item = new ListViewItem(f.Name);
                    item.Tag = f; // store FileInfo for comparison
                    item.SubItems.Add(FormatSizeInKb(f.Length));
                    item.SubItems.Add(f.LastWriteTime.ToString("g"));
                    lv.Items.Add(item);
                }
                // 컬럼 너비 자동 조정 (컨텐츠 기준)
                for (int i = 0; i < lv.Columns.Count; i++)
                {
                    lv.AutoResizeColumn(i,
                        ColumnHeaderAutoResizeStyle.ColumnContent);
                }
            }
            catch (DirectoryNotFoundException)
            {
                MessageBox.Show(this, "폴더를 찾을 수 없습니다.", "오류",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "입출력 오류: " + ex.Message, "오류",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                lv.EndUpdate();
            }
        }

        private static string FormatSizeInKb(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            var kb = bytes / 1024.0;
            if (kb < 1024)
                return kb.ToString("0.#") + " KB";
            var mb = kb / 1024.0;
            return mb.ToString("0.#") + " MB";
        }

        private static string MakeKey(FileSystemInfo fi)
        {
            // key based on name and creation time (UTC)
            return $"{fi.Name}|{fi.CreationTimeUtc.Ticks}";
        }

        private void CompareAndColor()
        {
            // Reset all to black by default
            foreach (ListViewItem it in lvwLeftDir.Items)
                it.ForeColor = Color.Black;
            foreach (ListViewItem it in lvwrightDir.Items)
                it.ForeColor = Color.Black;

            // build lookup for right items (files and directories) by name
            var rightLookup = lvwrightDir.Items.Cast<ListViewItem>()
                .Where(it => it.Tag is FileInfo || it.Tag is DirectoryInfo)
                .GroupBy(it => ((FileSystemInfo)it.Tag).Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            // process left items (files and directories)
            foreach (ListViewItem leftItem in lvwLeftDir.Items.Cast<ListViewItem>().ToList())
            {
                if (leftItem.Tag is FileInfo || leftItem.Tag is DirectoryInfo)
                {
                    var lfi = (FileSystemInfo)leftItem.Tag;
                    if (rightLookup.TryGetValue(lfi.Name, out var rights) && rights.Count > 0)
                    {
                        // try exact creation-time match
                        var match = rights.FirstOrDefault(r => ((FileSystemInfo)r.Tag).CreationTimeUtc.Ticks == lfi.CreationTimeUtc.Ticks);
                        if (match != null)
                        {
                            // identical
                            leftItem.ForeColor = Color.Black;
                            match.ForeColor = Color.Black;
                            rights.Remove(match);
                            if (rights.Count == 0) rightLookup.Remove(lfi.Name);
                        }
                        else
                        {
                            // same name but different creation time -> different
                            leftItem.ForeColor = Color.Gray; // Old
                            var r = rights[0];
                            r.ForeColor = Color.Red; // New
                            rights.RemoveAt(0);
                            if (rights.Count == 0) rightLookup.Remove(lfi.Name);
                        }
                    }
                    else
                    {
                        // no counterpart -> single-only
                        leftItem.ForeColor = Color.Purple;
                    }
                }
                else
                {
                    // other types remain default
                    leftItem.ForeColor = Color.Black;
                }
            }

            // any remaining right items are single-only
            foreach (var kv in rightLookup)
            {
                foreach (var rItem in kv.Value)
                {
                    rItem.ForeColor = Color.Purple;
                }
            }
        }

        // Recursively copy a directory and its contents
        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            var dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirName}");

            Directory.CreateDirectory(destDirName);

            // copy files
            foreach (var file in dir.GetFiles())
            {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // copy subdirectories
            foreach (var subdir in dir.GetDirectories())
            {
                var temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnLeftDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "폴더를 선택하세요.";
                // 현재 텍스트박스에 있는 경로를 초기 선택 폴더로 설정
                if (!string.IsNullOrWhiteSpace(txtLeftDir.Text) &&
                Directory.Exists(txtLeftDir.Text))
                {
                    dlg.SelectedPath = txtLeftDir.Text;
                }
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtLeftDir.Text = dlg.SelectedPath;
                    // 선택한 폴더의 항목을 리스트뷰에 표시
                    PopulateListView(lvwLeftDir, txtLeftDir.Text);
                    CompareAndColor();
                }
            }
        }

        private void btnRightDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "폴더를 선택하세요.";
                // 현재 텍스트박스에 있는 경로를 초기 선택 폴더로 설정
                if (!string.IsNullOrWhiteSpace(txtRightDir.Text) &&
                Directory.Exists(txtRightDir.Text))
                {
                    dlg.SelectedPath = txtRightDir.Text;
                }
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtRightDir.Text = dlg.SelectedPath;
                    // 선택한 폴더의 항목을 리스트뷰에 표시
                    PopulateListView(lvwrightDir, txtRightDir.Text);
                    CompareAndColor();
                }
            }
        }

        private void lvwLeftDir_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            // Simplified drawing: use default drawing to avoid referencing
            // file-comparison variables that may be defined elsewhere.
            e.DrawDefault = true;
        }

        private void lvwrightDir_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            // Simplified drawing
            e.DrawDefault = true;
        }

        private void btnCopyFromLeft_Click(object sender, EventArgs e)
        {
            var sel = lvwLeftDir.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            if (sel == null || !(sel.Tag is FileInfo || sel.Tag is DirectoryInfo))
            {
                MessageBox.Show("왼쪽에서 복사할 항목(파일 또는 폴더)을 선택하세요.", "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtRightDir.Text) || !Directory.Exists(txtRightDir.Text))
            {
                MessageBox.Show("오른쪽 대상 폴더를 먼저 선택하세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (sel.Tag is FileInfo lfi)
            {
                var destPath = Path.Combine(txtRightDir.Text, lfi.Name);
                try
                {
                    if (File.Exists(destPath))
                    {
                        var rfi = new FileInfo(destPath);
                        if (rfi.CreationTimeUtc.Ticks != lfi.CreationTimeUtc.Ticks)
                        {
                            var msg = $"파일명이 동일하지만 생성일이 다릅니다.\n왼쪽 생성일: {lfi.CreationTime:G}\n오른쪽 생성일: {rfi.CreationTime:G}\n\n덮어쓰시겠습니까?";
                            var dr = MessageBox.Show(msg, "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr != DialogResult.Yes) return;
                        }
                    }
                    // overwrite
                    File.Copy(lfi.FullName, destPath, true);
                    MessageBox.Show("복사 완료.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("복사 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (sel.Tag is DirectoryInfo ldi)
            {
                var destPath = Path.Combine(txtRightDir.Text, ldi.Name);
                try
                {
                    if (Directory.Exists(destPath))
                    {
                        var rdi = new DirectoryInfo(destPath);
                        if (rdi.CreationTimeUtc.Ticks != ldi.CreationTimeUtc.Ticks)
                        {
                            var msg = $"폴더명이 동일하지만 생성일이 다릅니다.\n왼쪽 생성일: {ldi.CreationTime:G}\n오른쪽 생성일: {rdi.CreationTime:G}\n\n덮어쓰시겠습니까?";
                            var dr = MessageBox.Show(msg, "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr != DialogResult.Yes) return;
                        }
                    }
                    // copy recursively; DirectoryCopy uses overwrite for files
                    DirectoryCopy(ldi.FullName, destPath);
                    MessageBox.Show("폴더 복사 완료.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("폴더 복사 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // refresh both lists
            if (!string.IsNullOrWhiteSpace(txtLeftDir.Text) && Directory.Exists(txtLeftDir.Text))
                PopulateListView(lvwLeftDir, txtLeftDir.Text);
            if (!string.IsNullOrWhiteSpace(txtRightDir.Text) && Directory.Exists(txtRightDir.Text))
                PopulateListView(lvwrightDir, txtRightDir.Text);
            CompareAndColor();
        }

        private void btnCopyFromRight_Click(object sender, EventArgs e)
        {
            var sel = lvwrightDir.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            if (sel == null || !(sel.Tag is FileInfo || sel.Tag is DirectoryInfo))
            {
                MessageBox.Show("오른쪽에서 복사할 항목(파일 또는 폴더)을 선택하세요.", "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtLeftDir.Text) || !Directory.Exists(txtLeftDir.Text))
            {
                MessageBox.Show("왼쪽 대상 폴더를 먼저 선택하세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (sel.Tag is FileInfo rfi)
            {
                var destPath = Path.Combine(txtLeftDir.Text, rfi.Name);
                try
                {
                    if (File.Exists(destPath))
                    {
                        var lfi = new FileInfo(destPath);
                        if (lfi.CreationTimeUtc.Ticks != rfi.CreationTimeUtc.Ticks)
                        {
                            var msg = $"파일명이 동일하지만 생성일이 다릅니다.\n오른쪽 생성일: {rfi.CreationTime:G}\n왼쪽 생성일: {lfi.CreationTime:G}\n\n덮어쓰시겠습니까?";
                            var dr = MessageBox.Show(msg, "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr != DialogResult.Yes) return;
                        }
                    }
                    File.Copy(rfi.FullName, destPath, true);
                    MessageBox.Show("복사 완료.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("복사 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (sel.Tag is DirectoryInfo rdi)
            {
                var destPath = Path.Combine(txtLeftDir.Text, rdi.Name);
                try
                {
                    if (Directory.Exists(destPath))
                    {
                        var ldi = new DirectoryInfo(destPath);
                        if (ldi.CreationTimeUtc.Ticks != rdi.CreationTimeUtc.Ticks)
                        {
                            var msg = $"폴더명이 동일하지만 생성일이 다릅니다.\n오른쪽 생성일: {rdi.CreationTime:G}\n왼쪽 생성일: {ldi.CreationTime:G}\n\n덮어쓰시겠습니까?";
                            var dr = MessageBox.Show(msg, "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr != DialogResult.Yes) return;
                        }
                    }
                    DirectoryCopy(rdi.FullName, destPath);
                    MessageBox.Show("폴더 복사 완료.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("폴더 복사 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // refresh both lists
            if (!string.IsNullOrWhiteSpace(txtLeftDir.Text) && Directory.Exists(txtLeftDir.Text))
                PopulateListView(lvwLeftDir, txtLeftDir.Text);
            if (!string.IsNullOrWhiteSpace(txtRightDir.Text) && Directory.Exists(txtRightDir.Text))
                PopulateListView(lvwrightDir, txtRightDir.Text);
            CompareAndColor();
        }

    }

}
