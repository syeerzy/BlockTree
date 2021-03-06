﻿using BlockTree.Core;
using BlockTree.Cryptography;
using BlockTree.Implementations.Blockchains.LevelDB;
using BlockTree.Implementations.Wallets.EntityFramework;
using BlockTree.Implementations.Wallets.NEP6;
using BlockTree.IO;
using BlockTree.Properties;
using BlockTree.SmartContract;
using BlockTree.VM;
using BlockTree.Wallets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using VMArray = BlockTree.VM.Types.Array;

namespace BlockTree.UI
{
    internal partial class MainForm : Form
    {
        private static readonly UInt160 RecycleScriptHash = new[] { (byte)OpCode.PUSHT }.ToScriptHash();
        private bool balance_changed = false;
        private bool check_nep5_balance = false;
        private DateTime persistence_time = DateTime.MinValue;

        public MainForm(XDocument xdoc = null)
        {
            InitializeComponent();
            if (xdoc != null)
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                Version latest = Version.Parse(xdoc.Element("update").Attribute("latest").Value);
                if (version < latest)
                {
                    toolStripStatusLabel3.Tag = xdoc;
                    toolStripStatusLabel3.Text += $": {latest}";
                    toolStripStatusLabel3.Visible = true;
                }
            }
        }

        private void AddAccount(WalletAccount account, bool selected = false)
        {
            ListViewItem item = listView1.Items[account.Address];
            if (item != null)
            {
                if (!account.WatchOnly && ((WalletAccount)item.Tag).WatchOnly)
                {
                    listView1.Items.Remove(item);
                    item = null;
                }
            }
            if (item == null)
            {
                string groupName = account.WatchOnly ? "watchOnlyGroup" : account.Contract.IsStandard ? "standardContractGroup" : "nonstandardContractGroup";
                item = listView1.Items.Add(new ListViewItem(new[]
                {
                    new ListViewItem.ListViewSubItem
                    {
                        Name = "address",
                        Text = account.Address
                    },
                    new ListViewItem.ListViewSubItem
                    {
                        Name = "ans"
                    },
                    new ListViewItem.ListViewSubItem
                    {
                        Name = "anc"
                    }
                }, -1, listView1.Groups[groupName])
                {
                    Name = account.Address,
                    Tag = account
                });
            }
            item.Selected = selected;
        }

        private void AddTransaction(Transaction tx, uint? height, uint time)
        {
            int? confirmations = (int)Blockchain.Default.Height - (int?)height + 1;
            if (confirmations <= 0) confirmations = null;
            string confirmations_str = confirmations?.ToString() ?? Strings.Unconfirmed;
            string txid = tx.Hash.ToString();
            if (listView3.Items.ContainsKey(txid))
            {
                listView3.Items[txid].Tag = height;
                listView3.Items[txid].SubItems["confirmations"].Text = confirmations_str;
            }
            else
            {
                listView3.Items.Insert(0, new ListViewItem(new[]
                {
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "time",
                                Text = time.ToDateTime().ToString()
                            },
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "hash",
                                Text = txid
                            },
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "confirmations",
                                Text = confirmations_str
                            },
                            //add transaction type to list by phinx
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "txtype",
                                Text = tx.Type.ToString()
                            }
                            //end

                        }, -1)
                {
                    Name = txid,
                    Tag = height
                });
            }
        }

        private void Blockchain_PersistCompleted(object sender, Block block)
        {
            if (IsDisposed) return;

            persistence_time = DateTime.UtcNow;
            if (Program.CurrentWallet != null)
            {
                check_nep5_balance = true;
                if (Program.CurrentWallet.GetCoins().Any(p => !p.State.HasFlag(CoinState.Spent) && p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)) == true)
                    balance_changed = true;
            }

            BeginInvoke(new Action(RefreshConfirmations));
        }

        private void ChangeWallet(Wallet wallet)
        {
            if (Program.CurrentWallet != null)
            {
                Program.CurrentWallet.BalanceChanged -= CurrentWallet_BalanceChanged;
                if (Program.CurrentWallet is IDisposable disposable)
                    disposable.Dispose();
            }
            Program.CurrentWallet = wallet;
            listView3.Items.Clear();
            if (Program.CurrentWallet != null)
            {
                foreach (var i in Program.CurrentWallet.GetTransactions().Select(p => new
                {
                    Transaction = Blockchain.Default.GetTransaction(p, out int height),
                    Height = (uint)height
                }).Where(p => p.Transaction != null).Select(p => new
                {
                    p.Transaction,
                    p.Height,
                    Time = Blockchain.Default.GetHeader(p.Height).Timestamp
                }).OrderBy(p => p.Time))
                {
                    AddTransaction(i.Transaction, i.Height, i.Time);
                }
                Program.CurrentWallet.BalanceChanged += CurrentWallet_BalanceChanged;
            }
            修改密码CToolStripMenuItem.Enabled = Program.CurrentWallet is UserWallet;
            交易TToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            提取燃料币CToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            signDataToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            requestCertificateToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            注册资产RToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            资产分发IToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            deployContractToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            invokeContractToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            选举EToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            创建新地址NToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            导入私钥IToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            创建智能合约SToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            listView1.Items.Clear();
            if (Program.CurrentWallet != null)
            {
                foreach (WalletAccount account in Program.CurrentWallet.GetAccounts().ToArray())
                {
                    AddAccount(account);
                }
            }
            balance_changed = true;
            check_nep5_balance = true;
        }

        private void CurrentWallet_BalanceChanged(object sender, BalanceEventArgs e)
        {
            balance_changed = true;
            BeginInvoke(new Action<Transaction, uint?, uint>(AddTransaction), e.Transaction, e.Height, e.Time);
        }

        private void ImportBlocks(Stream stream, bool read_start = false)
        {
            LevelDBBlockchain blockchain = (LevelDBBlockchain)Blockchain.Default;
            using (BinaryReader r = new BinaryReader(stream))
            {
                uint start = read_start ? r.ReadUInt32() : 0;
                uint count = r.ReadUInt32();
                uint end = start + count - 1;
                if (end <= blockchain.Height) return;
                for (uint height = start; height <= end; height++)
                {
                    byte[] array = r.ReadBytes(r.ReadInt32());
                    if (height > blockchain.Height)
                    {
                        Block block = array.AsSerializable<Block>();
                        blockchain.AddBlockDirectly(block);
                    }
                }
            }
        }

        private void RefreshConfirmations()
        {
            foreach (ListViewItem item in listView3.Items)
            {
                uint? height = item.Tag as uint?;
                int? confirmations = (int)Blockchain.Default.Height - (int?)height + 1;
                if (confirmations <= 0) confirmations = null;
                item.SubItems["confirmations"].Text = confirmations?.ToString() ?? Strings.Unconfirmed;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                const string path_acc = "chain.acc";
                if (File.Exists(path_acc))
                {
                    using (FileStream fs = new FileStream(path_acc, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        ImportBlocks(fs);
                    }
                }
                const string path_acc_zip = path_acc + ".zip";
                if (File.Exists(path_acc_zip))
                {
                    using (FileStream fs = new FileStream(path_acc_zip, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(path_acc).Open())
                    {
                        ImportBlocks(zs);
                    }
                }
                var paths = Directory.EnumerateFiles(".", "chain.*.acc", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(".", "chain.*.acc.zip", SearchOption.TopDirectoryOnly)).Select(p => new
                {
                    FileName = Path.GetFileName(p),
                    Start = uint.Parse(Regex.Match(p, @"\d+").Value),
                    IsCompressed = p.EndsWith(".zip")
                }).OrderBy(p => p.Start);
                foreach (var path in paths)
                {
                    if (path.Start > Blockchain.Default.Height + 1) break;
                    if (path.IsCompressed)
                    {
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.None))
                        using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                        using (Stream zs = zip.GetEntry(Path.GetFileNameWithoutExtension(path.FileName)).Open())
                        {
                            ImportBlocks(zs, true);
                        }
                    }
                    else
                    {
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            ImportBlocks(fs, true);
                        }
                    }
                }
                Blockchain.PersistCompleted += Blockchain_PersistCompleted;
                Program.LocalNode.Start(Settings.Default.P2P.Port, Settings.Default.P2P.WsPort);
            });
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Blockchain.PersistCompleted -= Blockchain_PersistCompleted;
            ChangeWallet(null);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            uint walletHeight = 0;

            if (Program.CurrentWallet != null)
            {
                walletHeight = (Program.CurrentWallet.WalletHeight > 0) ? Program.CurrentWallet.WalletHeight - 1 : 0;
            }

            lbl_height.Text = $"{walletHeight}/{Blockchain.Default.Height}/{Blockchain.Default.HeaderHeight}";

            lbl_count_node.Text = Program.LocalNode.RemoteNodeCount.ToString();
            TimeSpan persistence_span = DateTime.UtcNow - persistence_time;
            if (persistence_span < TimeSpan.Zero) persistence_span = TimeSpan.Zero;
            if (persistence_span > Blockchain.TimePerBlock)
            {
                toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                toolStripProgressBar1.Value = persistence_span.Seconds;
                toolStripProgressBar1.Style = ProgressBarStyle.Blocks;
            }
            if (Program.CurrentWallet != null)
            {
                if (Program.CurrentWallet.WalletHeight <= Blockchain.Default.Height + 1)
                {
                    if (balance_changed)
                    {
                        IEnumerable<Coin> coins = Program.CurrentWallet?.GetCoins().Where(p => !p.State.HasFlag(CoinState.Spent)) ?? Enumerable.Empty<Coin>();
                        Fixed10 bonus_available = Blockchain.CalculateBonus(Program.CurrentWallet.GetUnclaimedCoins().Select(p => p.Reference));
                        Fixed10 bonus_unavailable = Blockchain.CalculateBonus(coins.Where(p => p.State.HasFlag(CoinState.Confirmed) && p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)).Select(p => p.Reference), Blockchain.Default.Height + 1);
                        Fixed10 bonus = bonus_available + bonus_unavailable;
                        var assets = coins.GroupBy(p => p.Output.AssetId, (k, g) => new
                        {
                            Asset = Blockchain.Default.GetAssetState(k),
                            Value = g.Sum(p => p.Output.Value),
                            Claim = k.Equals(Blockchain.UtilityToken.Hash) ? bonus : Fixed10.Zero
                        }).ToDictionary(p => p.Asset.AssetId);
                        if (bonus != Fixed10.Zero && !assets.ContainsKey(Blockchain.UtilityToken.Hash))
                        {
                            assets[Blockchain.UtilityToken.Hash] = new
                            {
                                Asset = Blockchain.Default.GetAssetState(Blockchain.UtilityToken.Hash),
                                Value = Fixed10.Zero,
                                Claim = bonus
                            };
                        }
                        var balance_ans = coins.Where(p => p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)).GroupBy(p => p.Output.ScriptHash).ToDictionary(p => p.Key, p => p.Sum(i => i.Output.Value));
                        var balance_anc = coins.Where(p => p.Output.AssetId.Equals(Blockchain.UtilityToken.Hash)).GroupBy(p => p.Output.ScriptHash).ToDictionary(p => p.Key, p => p.Sum(i => i.Output.Value));
                        foreach (ListViewItem item in listView1.Items)
                        {
                            UInt160 script_hash = Wallet.ToScriptHash(item.Name);
                            Fixed10 ans = balance_ans.ContainsKey(script_hash) ? balance_ans[script_hash] : Fixed10.Zero;
                            Fixed10 anc = balance_anc.ContainsKey(script_hash) ? balance_anc[script_hash] : Fixed10.Zero;
                            item.SubItems["ans"].Text = ans.ToString();
                            item.SubItems["anc"].Text = anc.ToString();
                        }
                        foreach (AssetState asset in listView2.Items.OfType<ListViewItem>().Select(p => p.Tag as AssetState).Where(p => p != null).ToArray())
                        {
                            if (!assets.ContainsKey(asset.AssetId))
                            {
                                listView2.Items.RemoveByKey(asset.AssetId.ToString());
                            }
                        }
                        foreach (var asset in assets.Values)
                        {
                            string value_text = asset.Value.ToString() + (asset.Asset.AssetId.Equals(Blockchain.UtilityToken.Hash) ? $"+({asset.Claim})" : "");
                            if (listView2.Items.ContainsKey(asset.Asset.AssetId.ToString()))
                            {
                                listView2.Items[asset.Asset.AssetId.ToString()].SubItems["value"].Text = value_text;
                            }
                            else
                            {
                                string asset_name = asset.Asset.AssetType == AssetType.GoverningToken ? "Seed" :
                                                    asset.Asset.AssetType == AssetType.UtilityToken ? "Gas" :
                                                    asset.Asset.GetName();
                                listView2.Items.Add(new ListViewItem(new[]
                                {
                                    new ListViewItem.ListViewSubItem
                                    {
                                        Name = "name",
                                        Text = asset_name
                                    },
                                    new ListViewItem.ListViewSubItem
                                    {
                                        Name = "type",
                                        Text = asset.Asset.AssetType.ToString()
                                    },
                                    new ListViewItem.ListViewSubItem
                                    {
                                        Name = "value",
                                        Text = value_text
                                    },
                                    new ListViewItem.ListViewSubItem
                                    {
                                        ForeColor = Color.Gray,
                                        Name = "issuer",
                                        Text = $"{Strings.UnknownIssuer}[{asset.Asset.Owner}]"
                                    }
                                }, -1, listView2.Groups["unchecked"])
                                {
                                    Name = asset.Asset.AssetId.ToString(),
                                    Tag = asset.Asset,
                                    UseItemStyleForSubItems = false
                                });
                            }
                        }
                        balance_changed = false;
                    }
                    foreach (ListViewItem item in listView2.Groups["unchecked"].Items.OfType<ListViewItem>().ToArray())
                    {
                        ListViewItem.ListViewSubItem subitem = item.SubItems["issuer"];
                        AssetState asset = (AssetState)item.Tag;
                        CertificateQueryResult result;
                        if (asset.AssetType == AssetType.GoverningToken || asset.AssetType == AssetType.UtilityToken)
                        {
                            result = new CertificateQueryResult { Type = CertificateQueryResultType.System };
                        }
                        else
                        {
                            result = CertificateQueryService.Query(asset.Owner);
                        }
                        using (result)
                        {
                            subitem.Tag = result.Type;
                            switch (result.Type)
                            {
                                case CertificateQueryResultType.Querying:
                                case CertificateQueryResultType.QueryFailed:
                                    break;
                                case CertificateQueryResultType.System:
                                    subitem.ForeColor = Color.Green;
                                    subitem.Text = Strings.SystemIssuer;
                                    break;
                                case CertificateQueryResultType.Invalid:
                                    subitem.ForeColor = Color.Red;
                                    subitem.Text = $"[{Strings.InvalidCertificate}][{asset.Owner}]";
                                    break;
                                case CertificateQueryResultType.Expired:
                                    subitem.ForeColor = Color.Yellow;
                                    subitem.Text = $"[{Strings.ExpiredCertificate}]{result.Certificate.Subject}[{asset.Owner}]";
                                    break;
                                case CertificateQueryResultType.Good:
                                    subitem.ForeColor = Color.Black;
                                    subitem.Text = $"{result.Certificate.Subject}[{asset.Owner}]";
                                    break;
                            }
                            switch (result.Type)
                            {
                                case CertificateQueryResultType.System:
                                case CertificateQueryResultType.Missing:
                                case CertificateQueryResultType.Invalid:
                                case CertificateQueryResultType.Expired:
                                case CertificateQueryResultType.Good:
                                    item.Group = listView2.Groups["checked"];
                                    break;
                            }
                        }
                    }
                }
                if (check_nep5_balance && persistence_span > TimeSpan.FromSeconds(2))
                {
                    UInt160[] addresses = Program.CurrentWallet.GetAccounts().Select(p => p.ScriptHash).ToArray();
                    foreach (string s in Settings.Default.NEP5Watched)
                    {
                        UInt160 script_hash = UInt160.Parse(s);
                        byte[] script;
                        using (ScriptBuilder sb = new ScriptBuilder())
                        {
                            foreach (UInt160 address in addresses)
                                sb.EmitAppCall(script_hash, "balanceOf", address);
                            sb.Emit(OpCode.DEPTH, OpCode.PACK);
                            sb.EmitAppCall(script_hash, "decimals");
                            sb.EmitAppCall(script_hash, "name");
                            script = sb.ToArray();
                        }
                        ApplicationEngine engine = ApplicationEngine.Run(script);
                        if (engine.State.HasFlag(VMState.FAULT)) continue;
                        string name = engine.CurrentContext.EvaluationStack.Pop().GetString();
                        byte decimals = (byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                        BigInteger amount = ((VMArray)engine.CurrentContext.EvaluationStack.Pop()).Aggregate(BigInteger.Zero, (x, y) => x + y.GetBigInteger());
                        if (amount == 0)
                        {
                            listView2.Items.RemoveByKey(script_hash.ToString());
                            continue;
                        }
                        BigDecimal balance = new BigDecimal(amount, decimals);
                        string value_text = balance.ToString();
                        if (listView2.Items.ContainsKey(script_hash.ToString()))
                        {
                            listView2.Items[script_hash.ToString()].SubItems["value"].Text = value_text;
                        }
                        else
                        {
                            listView2.Items.Add(new ListViewItem(new[]
                            {
                                new ListViewItem.ListViewSubItem
                                {
                                    Name = "name",
                                    Text = name
                                },
                                new ListViewItem.ListViewSubItem
                                {
                                    Name = "type",
                                    Text = "NEP-5"
                                },
                                new ListViewItem.ListViewSubItem
                                {
                                    Name = "value",
                                    Text = value_text
                                },
                                new ListViewItem.ListViewSubItem
                                {
                                    ForeColor = Color.Gray,
                                    Name = "issuer",
                                    Text = $"ScriptHash:{script_hash}"
                                }
                            }, -1, listView2.Groups["checked"])
                            {
                                Name = script_hash.ToString(),
                                UseItemStyleForSubItems = false
                            });
                        }
                    }
                    check_nep5_balance = false;
                }
            }
        }

        private void 创建钱包数据库NToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateWalletDialog dialog = new CreateWalletDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                NEP6Wallet wallet = new NEP6Wallet(dialog.WalletPath);
                wallet.Unlock(dialog.Password);
                wallet.CreateAccount();
                wallet.Save();
                ChangeWallet(wallet);
                Settings.Default.LastWalletPath = dialog.WalletPath;
                Settings.Default.Save();
            }
        }

        private void 打开钱包数据库OToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenWalletDialog dialog = new OpenWalletDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                string path = dialog.WalletPath;
                Wallet wallet;
                if (Path.GetExtension(path) == ".db3")
                {
                    if (MessageBox.Show(Strings.MigrateWalletMessage, Strings.MigrateWalletCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                    {
                        string path_old = path;
                        path = Path.ChangeExtension(path_old, ".json");
                        NEP6Wallet nep6wallet;
                        try
                        {
                            nep6wallet = NEP6Wallet.Migrate(path, path_old, dialog.Password);
                        }
                        catch (CryptographicException)
                        {
                            MessageBox.Show(Strings.PasswordIncorrect);
                            return;
                        }
                        nep6wallet.Save();
                        nep6wallet.Unlock(dialog.Password);
                        wallet = nep6wallet;
                        MessageBox.Show($"{Strings.MigrateWalletSucceedMessage}\n{path}");
                    }
                    else
                    {
                        try
                        {
                            wallet = UserWallet.Open(path, dialog.Password);
                        }
                        catch (CryptographicException)
                        {
                            MessageBox.Show(Strings.PasswordIncorrect);
                            return;
                        }
                    }
                }
                else
                {
                    NEP6Wallet nep6wallet = new NEP6Wallet(path);
                    try
                    {
                        nep6wallet.Unlock(dialog.Password);
                    }
                    catch (CryptographicException)
                    {
                        MessageBox.Show(Strings.PasswordIncorrect);
                        return;
                    }
                    wallet = nep6wallet;
                }
                ChangeWallet(wallet);
                Settings.Default.LastWalletPath = path;
                Settings.Default.Save();
            }
        }

        private void 修改密码CToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ChangePasswordDialog dialog = new ChangePasswordDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                if (((UserWallet)Program.CurrentWallet).ChangePassword(dialog.OldPassword, dialog.NewPassword))
                    MessageBox.Show(Strings.ChangePasswordSuccessful);
                else
                    MessageBox.Show(Strings.PasswordIncorrect);
            }
        }

        private void 重建钱包数据库RToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView2.Items.Clear();
            listView3.Items.Clear();
            WalletIndexer.RebuildIndex();
        }

        private void 退出XToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void 转账TToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Transaction tx;
            UInt160 change_address;
            Fixed10 fee;
            using (TransferDialog dialog = new TransferDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
                change_address = dialog.ChangeAddress;
                fee = dialog.Fee;
            }
            if (tx is InvocationTransaction itx)
            {
                using (InvokeContractDialog dialog = new InvokeContractDialog(itx))
                {
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    tx = dialog.GetTransaction(change_address, fee);
                }
            }
            Helper.SignAndShowInformation(tx);
        }

        private void 交易TToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (TradeForm form = new TradeForm())
            {
                form.ShowDialog();
            }
        }

        private void 签名SToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SigningTxDialog dialog = new SigningTxDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void 提取燃料币CToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Helper.Show<ClaimForm>();
        }

        private void requestCertificateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CertificateRequestWizard wizard = new UI.CertificateRequestWizard())
            {
                wizard.ShowDialog();
            }
        }

        private void 注册资产RToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InvocationTransaction tx;
            using (AssetRegisterDialog dialog = new AssetRegisterDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
            }
            using (InvokeContractDialog dialog = new InvokeContractDialog(tx))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
            }
            Helper.SignAndShowInformation(tx);
        }

        private void 资产分发IToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (IssueDialog dialog = new IssueDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Helper.SignAndShowInformation(dialog.GetTransaction());
            }
        }

        private void deployContractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InvocationTransaction tx;
            using (DeployContractDialog dialog = new DeployContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
            }
            using (InvokeContractDialog dialog = new InvokeContractDialog(tx))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
            }
            Helper.SignAndShowInformation(tx);
        }

        private void invokeContractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (InvokeContractDialog dialog = new InvokeContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Helper.SignAndShowInformation(dialog.GetTransaction());
            }
        }

        private void 选举EToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ElectionDialog dialog = new ElectionDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Helper.SignAndShowInformation(dialog.GetTransaction());
            }
        }

        private void signDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SigningDialog dialog = new SigningDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OptionsDialog dialog = new OptionsDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void 官网WToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://BlockTree.org/");
        }

        private void 开发人员工具TToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Helper.Show<DeveloperToolsForm>();
        }

        private void 关于BlockTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"{Strings.AboutMessage} {Strings.AboutVersion}{Assembly.GetExecutingAssembly().GetName().Version}", Strings.About);
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            查看私钥VToolStripMenuItem.Enabled =
                listView1.SelectedIndices.Count == 1 &&
                !((WalletAccount)listView1.SelectedItems[0].Tag).WatchOnly &&
                ((WalletAccount)listView1.SelectedItems[0].Tag).Contract.IsStandard;
            viewContractToolStripMenuItem.Enabled =
                listView1.SelectedIndices.Count == 1 &&
                !((WalletAccount)listView1.SelectedItems[0].Tag).WatchOnly;
            voteToolStripMenuItem.Enabled =
                listView1.SelectedIndices.Count == 1 &&
                !((WalletAccount)listView1.SelectedItems[0].Tag).WatchOnly &&
                !string.IsNullOrEmpty(listView1.SelectedItems[0].SubItems["ans"].Text) &&
                decimal.Parse(listView1.SelectedItems[0].SubItems["ans"].Text) > 0;
            复制到剪贴板CToolStripMenuItem.Enabled = listView1.SelectedIndices.Count == 1;
            删除DToolStripMenuItem.Enabled = listView1.SelectedIndices.Count > 0;
        }

        private void 创建新地址NToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.SelectedIndices.Clear();
            WalletAccount account = Program.CurrentWallet.CreateAccount();
            AddAccount(account, true);
            if (Program.CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
        }

        private void importWIFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ImportPrivateKeyDialog dialog = new ImportPrivateKeyDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                listView1.SelectedIndices.Clear();
                foreach (string wif in dialog.WifStrings)
                {
                    WalletAccount account;
                    try
                    {
                        account = Program.CurrentWallet.Import(wif);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    AddAccount(account, true);
                }
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
            }
        }

        private void importCertificateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SelectCertificateDialog dialog = new SelectCertificateDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                listView1.SelectedIndices.Clear();
                WalletAccount account = Program.CurrentWallet.Import(dialog.SelectedCertificate);
                AddAccount(account, true);
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
            }
        }

        private void importWatchOnlyAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = InputBox.Show(Strings.Address, Strings.ImportWatchOnlyAddress);
            if (string.IsNullOrEmpty(text)) return;
            using (StringReader reader = new StringReader(text))
            {
                while (true)
                {
                    string address = reader.ReadLine();
                    if (address == null) break;
                    address = address.Trim();
                    if (string.IsNullOrEmpty(address)) continue;
                    UInt160 scriptHash;
                    try
                    {
                        scriptHash = Wallet.ToScriptHash(address);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    WalletAccount account = Program.CurrentWallet.CreateAccount(scriptHash);
                    AddAccount(account, true);
                }
            }
            if (Program.CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
        }

        private void 多方签名MToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateMultiSigContractDialog dialog = new CreateMultiSigContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Contract contract = dialog.GetContract();
                if (contract == null)
                {
                    MessageBox.Show(Strings.AddContractFailedMessage);
                    return;
                }
                WalletAccount account = Program.CurrentWallet.CreateAccount(contract, dialog.GetKey());
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
                listView1.SelectedIndices.Clear();
                AddAccount(account, true);
            }
        }

        private void lockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateLockAccountDialog dialog = new CreateLockAccountDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Contract contract = dialog.GetContract();
                if (contract == null)
                {
                    MessageBox.Show(Strings.AddContractFailedMessage);
                    return;
                }
                WalletAccount account = Program.CurrentWallet.CreateAccount(contract, dialog.GetKey());
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
                listView1.SelectedIndices.Clear();
                AddAccount(account, true);
            }
        }

        private void 自定义CToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ImportCustomContractDialog dialog = new ImportCustomContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Contract contract = dialog.GetContract();
                WalletAccount account = Program.CurrentWallet.CreateAccount(contract, dialog.GetKey());
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
                listView1.SelectedIndices.Clear();
                AddAccount(account, true);
            }
        }

        private void 查看私钥VToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalletAccount account = (WalletAccount)listView1.SelectedItems[0].Tag;
            using (ViewPrivateKeyDialog dialog = new ViewPrivateKeyDialog(account))
            {
                dialog.ShowDialog();
            }
        }

        private void viewContractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalletAccount account = (WalletAccount)listView1.SelectedItems[0].Tag;
            using (ViewContractDialog dialog = new ViewContractDialog(account.Contract))
            {
                dialog.ShowDialog();
            }
        }

        private void voteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalletAccount account = (WalletAccount)listView1.SelectedItems[0].Tag;
            using (VotingDialog dialog = new VotingDialog(account.ScriptHash))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Helper.SignAndShowInformation(dialog.GetTransaction());
            }
        }

        private void 复制到剪贴板CToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(listView1.SelectedItems[0].Text);
            }
            catch (ExternalException) { }
        }

        private void 删除DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(Strings.DeleteAddressConfirmationMessage, Strings.DeleteAddressConfirmationCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            WalletAccount[] accounts = listView1.SelectedItems.OfType<ListViewItem>().Select(p => (WalletAccount)p.Tag).ToArray();
            foreach (WalletAccount account in accounts)
            {
                listView1.Items.RemoveByKey(account.Address);
                Program.CurrentWallet.DeleteAccount(account.ScriptHash);
            }
            if (Program.CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
            balance_changed = true;
            check_nep5_balance = true;
        }

        private void contextMenuStrip2_Opening(object sender, CancelEventArgs e)
        {
            viewCertificateToolStripMenuItem.Enabled = listView2.SelectedIndices.Count == 1;
            if (viewCertificateToolStripMenuItem.Enabled)
            {
                CertificateQueryResultType? type = (CertificateQueryResultType?)listView2.SelectedItems[0].SubItems["issuer"].Tag;
                viewCertificateToolStripMenuItem.Enabled = type == CertificateQueryResultType.Good || type == CertificateQueryResultType.Expired || type == CertificateQueryResultType.Invalid;
            }
            删除DToolStripMenuItem1.Enabled = listView2.SelectedIndices.Count > 0;
            if (删除DToolStripMenuItem1.Enabled)
            {
                删除DToolStripMenuItem1.Enabled = listView2.SelectedItems.OfType<ListViewItem>().Select(p => p.Tag as AssetState).All(p => p == null || (p.AssetType != AssetType.GoverningToken && p.AssetType != AssetType.UtilityToken));
            }
        }

        private void viewCertificateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssetState asset = (AssetState)listView2.SelectedItems[0].Tag;
            UInt160 hash = Contract.CreateSignatureRedeemScript(asset.Owner).ToScriptHash();
            string address = Wallet.ToAddress(hash);
            string path = Path.Combine(Settings.Default.Paths.CertCache, $"{address}.cer");
            Process.Start(path);
        }

        private void 删除DToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count == 0) return;
            var delete = listView2.SelectedItems.OfType<ListViewItem>().Select(p => p.Tag as AssetState).Where(p => p != null).Select(p => new
            {
                Asset = p,
                Value = Program.CurrentWallet.GetAvailable(p.AssetId)
            }).ToArray();
            if (delete.Length == 0) return;
            if (MessageBox.Show($"{Strings.DeleteAssetConfirmationMessage}\n"
                + string.Join("\n", delete.Select(p => $"{p.Asset.GetName()}:{p.Value}"))
                , Strings.DeleteConfirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            ContractTransaction tx = Program.CurrentWallet.MakeTransaction(new ContractTransaction
            {
                Outputs = delete.Select(p => new TransactionOutput
                {
                    AssetId = p.Asset.AssetId,
                    Value = p.Value,
                    ScriptHash = RecycleScriptHash
                }).ToArray()
            }, fee: Fixed10.Zero);
            Helper.SignAndShowInformation(tx);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (listView3.SelectedItems.Count == 0) return;
            Clipboard.SetDataObject(listView3.SelectedItems[0].SubItems[1].Text);
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0) return;
            string url = string.Format(Settings.Default.Urls.AddressUrl, listView1.SelectedItems[0].Text);
            Process.Start(url);
        }

        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count == 0) return;
            string url = string.Format(Settings.Default.Urls.AssetUrl, listView2.SelectedItems[0].Name.Substring(2));
            Process.Start(url);
        }

        private void listView3_DoubleClick(object sender, EventArgs e)
        {
            if (listView3.SelectedIndices.Count == 0) return;
            string url = string.Format(Settings.Default.Urls.TransactionUrl, listView3.SelectedItems[0].Name.Substring(2));
            Process.Start(url);
        }

        private void toolStripStatusLabel3_Click(object sender, EventArgs e)
        {
            using (UpdateDialog dialog = new UpdateDialog((XDocument)toolStripStatusLabel3.Tag))
            {
                dialog.ShowDialog();
            }
        }
    }
}
