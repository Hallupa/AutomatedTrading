using System;
using System.Collections.Generic;
using System.Linq;
using Hallupa.TraderTools.Basics;
using TraderTools.Basics;

namespace StrategyEditor
{
    internal static class AssetBalanceUpdater
    {
        public static void UpdateAssetBalance(this Dictionary<string, AssetBalance> assetBalances, Trade trade)
        {
            /* TODO if (trade.TradeDirection == TradeDirection.Long && trade.EntryQuantity != null && trade.EntryPrice != null)
            {
                // Entry quantity already has commission taken off
                assetBalances.AddToAssetBalance(trade.BaseAsset, trade.EntryQuantity.Value);

                assetBalances.AddToAssetBalance(trade.GetQuoteAsset(), -trade.EntryQuantity.Value * trade.EntryPrice.Value);

                if (trade.Commission != null)
                {
                    if (trade.CommissionAsset != trade.GetQuoteAsset())
                        throw new ApplicationException("Commission asset must be quote asset");

                    assetBalances.AddToAssetBalance(trade.GetQuoteAsset(), -trade.Commission.Value);
                }
            }

            if (trade.TradeDirection == TradeDirection.Short && trade.EntryQuantity != null && trade.EntryPrice != null)
            {
                // Entry quantity already has commission taken off
                assetBalances.AddToAssetBalance(trade.BaseAsset, -trade.EntryQuantity.Value);
                assetBalances.AddToAssetBalance(trade.GetQuoteAsset(), trade.EntryQuantity.Value * trade.EntryPrice.Value);

                if (trade.Commission != null)
                {
                    if (trade.CommissionAsset != trade.GetQuoteAsset())
                        throw new ApplicationException("Commission asset must be quote asset");

                    assetBalances.AddToAssetBalance(trade.GetQuoteAsset(), -trade.Commission.Value);
                }
            }

            if (assetBalances.Any(x => x.Value.Balance < -0.0001M))
            {
                throw new ApplicationException("Asset balance below zero");
            }*/
        }

        private static void AddToAssetBalance(this Dictionary<string, AssetBalance> assetBalances, string asset, decimal amount)
        {
            if (!assetBalances.TryGetValue(asset, out var assetBalance))
            {
                assetBalances[asset] = (assetBalance = new AssetBalance(asset, amount));
                return;
            }

            assetBalances[asset] = new AssetBalance(asset, assetBalance.Balance + amount);
        }
    }
}