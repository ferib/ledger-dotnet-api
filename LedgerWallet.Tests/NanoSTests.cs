﻿using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;


namespace LedgerWallet.Tests
{
	[Trait("NanoS", "NanoS")]
    public class NanoSTests
    {

        [Fact]
        public async Task LedgerIsThreadSafe()
        {
            var ledger = (await LedgerClient.GetHIDLedgersAsync()).First();

            var tasks = new List<Task>();

            for(var i = 0; i < 50; i++)
            {
                tasks.Add(ledger.GetWalletPubKeyAsync(new KeyPath("1'/0")));
                tasks.Add(ledger.GetFirmwareVersionAsync());
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public async Task CanSignTransactionStandardMode()
        {
            await CanSignTransactionStandardModeCore(true);
            await CanSignTransactionStandardModeCore(false);
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public async Task CanGetWalletPubKey()
        {
            var ledger = (await LedgerClient.GetHIDLedgersAsync()).First();
            var firmwareVersion = await ledger.GetFirmwareVersionAsync();
            var path = new KeyPath("1'/0");
            var walletPubKeyResponse = await ledger.GetWalletPubKeyAsync(path, LedgerClient.AddressType.Legacy, true);
            await ledger.GetWalletPubKeyAsync(path, LedgerClient.AddressType.NativeSegwit, false);
            await ledger.GetWalletPubKeyAsync(path, LedgerClient.AddressType.Segwit, false);
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public async Task GetCoinVersion()
        {
            var ledger = (await LedgerClient.GetHIDLedgersAsync()).First();
            var coinVersion = await ledger.GetCoinVersion();
        }

        private async Task CanSignTransactionStandardModeCore(bool segwit)
        {
            var ledger = (await LedgerClient.GetHIDLedgersAsync()).First();
            var walletPubKey = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/0"));
            var address = segwit ? walletPubKey.UncompressedPublicKey.Compress().WitHash.ScriptPubKey : walletPubKey.GetAddress(network).ScriptPubKey;

            var response = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/1"));
            var changeAddress = response.GetAddress(network);

            var funding = network.Consensus.ConsensusFactory.CreateTransaction();
            funding.Inputs.Add(Network.Main.GetGenesis().Transactions[0].Inputs[0]);
            funding.Outputs.Add(new TxOut(Money.Coins(1.1m), address));
            funding.Outputs.Add(new TxOut(Money.Coins(1.0m), address));
            funding.Outputs.Add(new TxOut(Money.Coins(1.2m), address));

            var coins = funding.Outputs.AsCoins();

            var spending = network.Consensus.ConsensusFactory.CreateTransaction();
            spending.LockTime = 1;
            spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, Script.Empty)));
            spending.Inputs[0].Sequence = 1;
            spending.Outputs.Add(new TxOut(Money.Coins(0.5m), BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe", Network.Main)));
            spending.Outputs.Add(new TxOut(Money.Coins(0.8m), changeAddress));
            spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));


            var requests = new SignatureRequest[]{
                new SignatureRequest()
                {
                    InputCoin = new Coin(funding, 0),
                    InputTransaction = funding,
                    KeyPath = new KeyPath("1'/0")
                },
                new SignatureRequest()
                {
                    InputCoin = new Coin(funding, 1),
                    InputTransaction = funding,
                    KeyPath = new KeyPath("1'/0")
                },
                new SignatureRequest()
                {
                    InputCoin = new Coin(funding, 2),
                    InputTransaction = funding,
                    KeyPath = new KeyPath("1'/0")
                },
            };

            if(segwit)
            {
                foreach(var req in requests)
                    req.InputTransaction = null;
            }

            //should show 0.5 and 2.0 btc in fee
            var signed = await ledger.SignTransactionAsync(requests, spending, new KeyPath("1'/1"));
            //Assert.Equal(Script.Empty, spending.Inputs.Last().ScriptSig);
            Assert.NotNull(signed);
        }

	    readonly Network network = Network.Main;

        [Fact]
        [Trait("Manual", "Manual")]
        public async Task CanSignTransactionStandardModeConcurrently()
        {
            var ledger = (await LedgerClient.GetHIDLedgersAsync()).First();

            var walletPubKey = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/0"));
            var address = walletPubKey.GetAddress(network);

            var walletPubKey2 = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/1"));
            var changeAddress = walletPubKey2.GetAddress(network);

            var funding = network.Consensus.ConsensusFactory.CreateTransaction();
            funding.Inputs.Add(network.GetGenesis().Transactions[0].Inputs[0]);
            funding.Outputs.Add(new TxOut(Money.Coins(1.1m), address));
            funding.Outputs.Add(new TxOut(Money.Coins(1.0m), address));
            funding.Outputs.Add(new TxOut(Money.Coins(1.2m), address));

            var coins = funding.Outputs.AsCoins();

            var spending = network.Consensus.ConsensusFactory.CreateTransaction();
            spending.LockTime = 1;
            spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, o.ScriptPubKey)));
            spending.Outputs.Add(new TxOut(Money.Coins(0.5m), BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe", Network.Main)));
            spending.Outputs.Add(new TxOut(Money.Coins(0.8m), changeAddress));
            spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));

            var tasks = new List<Task>();

            for(var i = 0; i < 5; i++)
            {
                //should show 0.5 and 2.0 btc in fee
                var signed = ledger.SignTransactionAsync(
                  new KeyPath("1'/0"),
                  new Coin[]
                {
                new Coin(funding, 0),
                new Coin(funding, 1),
                new Coin(funding, 2),
                }, new Transaction[]
                {
                funding
                }, spending, new KeyPath("1'/1"));

                tasks.Add(signed);
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public async Task CanSignMultipleTransactionOutputs()
        {
            var ledger = (await LedgerClient.GetHIDLedgersAsync()).First();

            var walletPubKey = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/0"));
            var address = walletPubKey.GetAddress(network);

            var walletPubKey2 = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/1"));
            var changeAddress = walletPubKey2.GetAddress(network);

            var funding = network.Consensus.ConsensusFactory.CreateTransaction();
            funding.Inputs.Add(network.GetGenesis().Transactions[0].Inputs[0]);
            funding.Outputs.Add(new TxOut(Money.Coins(5m), address));
            funding.Outputs.Add(new TxOut(Money.Coins(0.4m), address));
            funding.Outputs.Add(new TxOut(Money.Coins(0.6m), address));

            var coins = funding.Outputs.AsCoins();

            var spending = network.Consensus.ConsensusFactory.CreateTransaction();
            spending.LockTime = 1;
            spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, o.ScriptPubKey)));

            // NOTE: having 3+ will promt for suspicious path, but won't sign anything :( 
            spending.Outputs.Add(new TxOut(Money.Coins(1m), BitcoinAddress.Create("bc1qc8h9tmkejfzzky79euxx5acmv9xthmcnk9df0m", Network.Main)));
            spending.Outputs.Add(new TxOut(Money.Coins(1m), BitcoinAddress.Create("bc1qve2w630azhahrhtmu047prnjjzxy2rymtd06na", Network.Main)));
            spending.Outputs.Add(new TxOut(Money.Coins(1m), BitcoinAddress.Create("bc1qdj59eexd2ggf4qa7u4n3fx9anurs5ad92d2jp3", Network.Main)));

            spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));

            var tasks = new List<Task>();

            for (var i = 0; i < 5; i++)
            {
                var signed = ledger.SignTransactionAsync(
                  new KeyPath("1'/0"),
                  new Coin[]
                {
                new Coin(funding, 0),
                new Coin(funding, 1),
                new Coin(funding, 2),
                }, new Transaction[]
                {
                funding
                }, spending, new KeyPath("1'/1"));

                tasks.Add(signed);
            }

            await Task.WhenAll(tasks);
        }
    }
}
