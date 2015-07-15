using Caliburn.Micro;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace ResEditor.ViewModels {
    public class EditorViewModel : Screen {

        public string ResFilePath {
            get;
            set;
        }

        private Dictionary<string, string> ResFiles {
            get;
            set;
        }

        public string DllFilePath {
            get;
            set;
        }

        public Type TmpType {
            get;
            set;
        }

        public BindableCollection<dynamic> Datas {
            get;
            set;
        }

        public List<Type> EntityTypes {
            get;
            set;
        }

        public Type SelectedEntity {
            get;
            set;
        }

        public ICollectionView CV {
            get;
            set;
        }

        /// <summary>
        /// 历史资源文件位置
        /// </summary>
        public List<string> HistoryResFiles {
            get;
            set;
        }

        /// <summary>
        /// 历史DLL文件位置
        /// </summary>
        public List<string> HistoryDllFiles {
            get;
            set;
        }

        private static readonly string HistoryResFile = Path.Combine(Environment.CurrentDirectory, "historyRes");
        private static readonly string HistoryDllFile = Path.Combine(Environment.CurrentDirectory, "historyDll");


        private string filterText = "";
        public string FilterText {
            get {
                return this.filterText;
            }
            set {
                this.filterText = value;
                if (this.CV != null) {
                    this.CV.Refresh();
                }
            }
        }

        public EditorViewModel() {
            this.LoadHistory();
        }

        private void LoadHistory() {
            if (File.Exists(HistoryResFile)) {
                var lines = File.ReadAllLines(HistoryResFile).Take(15);
                this.HistoryResFiles = lines.Where(l => l.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)).ToList();
                this.NotifyOfPropertyChange(() => this.HistoryResFiles);
            }

            if (File.Exists(HistoryDllFile)) {
                var lines = File.ReadAllLines(HistoryDllFile).Take(15);
                this.HistoryDllFiles = lines.Where(l => l.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
                this.NotifyOfPropertyChange(() => this.HistoryDllFiles);
            }
        }

        private bool Filter(dynamic o) {
            if (string.IsNullOrWhiteSpace((this.FilterText))) {
                return true;
            } else {
                return o.Key.IndexOf(this.FilterText.Trim(), StringComparison.OrdinalIgnoreCase) > -1;
            }
        }


        private void WriteHistory(string history, string filePath) {
            try {
                if (!File.Exists(history)) {
                    using (var file = File.Create(history)) {

                    }
                }

                var lines = File.ReadAllLines(history).Take(15).ToList();
                lines.Insert(0, filePath);
                lines = lines.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                File.WriteAllLines(history, lines);
            } catch {
            }

            this.LoadHistory();
        }

        #region resx
        public void ChoiceResxFile() {
            var dialog = new OpenFileDialog() {
                Filter = "Resx|*.resx"
            };

            try {
                dialog.InitialDirectory = Path.GetDirectoryName(this.ResFilePath);
                dialog.FileName = this.ResFilePath;
            } catch {
            }

            dialog.FileName = this.ResFilePath;
            if (dialog.ShowDialog() == true) {
                this.ResFilePath = dialog.FileName;
                this.NotifyOfPropertyChange(() => this.ResFilePath);

                this.ResFiles = this.GetLangFiles(dialog.FileName);
                this.DefineType(this.ResFiles.Keys);
                Task.Factory.StartNew(() => {
                    this.ReadResx(this.ResFiles);
                });

                this.WriteHistory(HistoryResFile, dialog.FileName);
            }
        }

        private void DefineType(IEnumerable<string> langs) {
            var tb = TypeBuilderHelper.Define("TTT", typeof(Record));
            langs.ToList().ForEach(l => {
                tb.DefineProperty(l.Replace("-", "_"), typeof(string));
            });
            this.TmpType = tb.CreateType();
        }

        private string GetResFileName(string path) {
            var onlyName = Path.GetFileNameWithoutExtension(path);
            var tmp = onlyName.Split(new char[] { '.' });
            if (tmp.Length > 1 && this.IsLang(tmp.Last())) {
                onlyName = string.Join(".", tmp.Take(tmp.Length - 1));
            }
            return onlyName;
        }

        private Dictionary<string, string> GetLangFiles(string path) {
            var name = this.GetResFileName(path);
            var dir = Path.GetDirectoryName(path);
            var files = Directory.GetFiles(dir, string.Format("{0}.*.resx", name));

            var dic = new Dictionary<string, string>() { 
                {"Default", Path.Combine(dir, string.Format("{0}.resx", name)) }
            };

            foreach (var f in files) {
                var lang = Regex.Match(f, @".(?<lang>[^. ]*?).resx").Groups["lang"].Value;
                dic.Add(lang, f);
            }

            return dic;
        }

        private bool IsLang(string lang) {
            try {
                CultureInfo.GetCultureInfo(lang);
                return true;
            } catch {
                return false;
            }
        }

        private void ReadResx(Dictionary<string, string> dic) {

            List<dynamic> results = new List<dynamic>();

            foreach (var d in dic) {
                try {

                    using (var reader = new ResXResourceReader(d.Value)) {
                        foreach (DictionaryEntry entry in reader) {
                            try {

                                if (entry.Key == null || entry.Value == null || entry.Key.GetType() != typeof(string) || entry.Value.GetType() != typeof(string))
                                    continue;

                                var key = (string)entry.Key;

                                var o = results.FirstOrDefault(r => r.Key.Equals(key));
                                if (o == null) {
                                    o = Activator.CreateInstance(this.TmpType);
                                    o.Key = (string)entry.Key;
                                    o.IsExists = true;
                                    results.Add(o);
                                }

                                this.TmpType.GetProperty(d.Key.Replace("-", "_"))
                                    .SetValue(o, (string)entry.Value);
                            } catch {
                            }
                        }
                    }
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message);
                }
            }

            if (results.Count == 0)
                results.Add(Activator.CreateInstance(this.TmpType));

            App.Current.Dispatcher.Invoke(() => {
                this.Datas = new BindableCollection<dynamic>(results);
                this.NotifyOfPropertyChange(() => this.Datas);

                this.CV = CollectionViewSource.GetDefaultView(this.Datas);
                this.CV.Filter = new Predicate<object>(this.Filter);
                this.CV.GroupDescriptions.Add(new PropertyGroupDescription("Key") {
                    Converter = new Converter1()
                });
                this.CV.SortDescriptions.Add(new SortDescription("Key", ListSortDirection.Ascending));
                this.NotifyOfPropertyChange(() => this.CV);
            }, DispatcherPriority.Send);
        }
        #endregion

        #region dll
        public void ChoiceDllFile() {
            var dialog = new OpenFileDialog() {
                Filter = "DLL|*.dll"
            };

            try {
                dialog.FileName = this.DllFilePath;
                dialog.InitialDirectory = Path.GetDirectoryName(this.DllFilePath);
            } catch {
            }

            if (dialog.ShowDialog() == true) {
                this.DllFilePath = dialog.FileName;
                this.NotifyOfPropertyChange(() => this.DllFilePath);
                Task.Factory.StartNew(() => {
                    this.LoadDll(dialog.FileName);
                });

                this.WriteHistory(HistoryDllFile, dialog.FileName);
            }
        }

        private void LoadDll(string file) {
            try {
                var asm = Assembly.LoadFrom(file);
                this.EntityTypes = asm.GetTypes().OrderBy(t => t.FullName).ToList();
                this.NotifyOfPropertyChange(() => this.EntityTypes);
            } catch {
            }
        }

        public void LoadKeysFromDll() {

            if (this.Datas != null)
                this.Datas.RemoveRange(this.Datas.Where(d => !d.IsExists).ToList());
            else
                return;

            if (this.SelectedEntity != null)
                this.SelectedEntity.GetProperties().ToList()
                    .ForEach(p => {
                        var key = string.Format("{0}_{1}_DisplayName", this.SelectedEntity.FullName.Replace(".", ""), p.Name);
                        var exists = this.Datas.Any(d => d.Key.Equals(key));
                        if (!exists) {
                            dynamic o = Activator.CreateInstance(this.TmpType);
                            o.Key = key;
                            o.EntityProperty = p.Name;

                            this.Datas.Add(o);
                        }
                    });

            this.NotifyOfPropertyChange(() => this.Datas);
        }
        #endregion


        #region save
        public void Save() {

            if (this.ResFiles == null || this.Datas == null)
                return;

            Dictionary<string, ResXResourceWriter> writers = new Dictionary<string, ResXResourceWriter>();
            foreach (var f in this.ResFiles) {
                writers.Add(f.Key, new ResXResourceWriter(f.Value));
            }

            foreach (var d in this.Datas) {

                if (string.IsNullOrEmpty(d.Key))
                    continue;

                var writer = writers.First(w => w.Key == "Default");
                writer.Value.AddResource(d.Key, (string)d.Default);

                foreach (var w in writers.Where(ww => !ww.Key.Equals("Default"))) {
                    var k = w.Key.Replace("-", "_");
                    PropertyInfo p = d.GetType().GetProperty(k);
                    if (p != null) {
                        var value = (string)p.GetValue(d);
                        w.Value.AddResource(d.Key, value);
                    }
                }

                d.IsExists = true;
            }

            foreach (var w in writers) {
                w.Value.Dispose();
            }
        }
        #endregion
    }
}
