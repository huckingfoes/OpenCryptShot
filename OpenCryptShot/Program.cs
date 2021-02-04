using System;
using System.Collections.Generic;
using System.IO;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using Newtonsoft.Json;

namespace OpenCryptShot
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "OpenCryptShot";
            Utilities.Write(ConsoleColor.Cyan,
                "\n   ___                    ___                 _   __ _           _   \n  /___\\_ __   ___ _ __   / __\\ __ _   _ _ __ | |_/ _\\ |__   ___ | |_ \n //  // '_ \\ / _ \\ '_ \\ / / | '__| | | | '_ \\| __\\ \\| '_ \\ / _ \\| __|\n/ \\_//| |_) |  __/ | | / /__| |  | |_| | |_) | |__\\ \\ | | | (_) | |_ \n\\___/ | .__/ \\___|_| |_\\____/_|   \\__, | .__/ \\__\\__/_| |_|\\___/ \\__|\n      |_|                         |___/|_|                           \n");

            Config config = LoadOrCreateConfig();
            if (config == null)
            {
                Console.Read();
                return;
            }

            if (config.takeProfitRate < 1)
            {
                Utilities.Write(ConsoleColor.Yellow, "Warning! takeProfitRate was below 1.0. It has been set to 1.0. This could have unwanted consequences.");
                config.takeProfitRate = 1;
            }

            if (config.stopLossRate > 1)
            {
                Utilities.Write(ConsoleColor.Yellow, "Warning! stopLossRate was over 1.0. It has been set to 1.0. This could have unwanted consequences.");
                config.stopLossRate = 1;
            }
            
            try
            {
                BinanceClient.SetDefaultOptions(new BinanceClientOptions
                {
                    ApiCredentials = new ApiCredentials(config.apiKey, config.apiSecret),
                    LogVerbosity = LogVerbosity.Debug,
                    LogWriters = new List<TextWriter> { Console.Out }
                });
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR! Could not set Binance options. Error message: {ex.Message}");
                Console.Read();
                return;
            }

            Utilities.Write(ConsoleColor.Green, "Successfully logged in.");

            while (true)
            {
                //Wait for symbol input
                Utilities.Write(ConsoleColor.Yellow, "Input your pair:");
                Console.ForegroundColor = ConsoleColor.White;
                string symbol = Console.ReadLine();

                //Exit the program if nothing was entered
                if (string.IsNullOrEmpty(symbol))
                    return;

                //Try to execute the order
                ExecuteOrder(symbol, config.quantity, config.takeProfitRate, config.stopLossRate);
            }
        }

        private static Config LoadOrCreateConfig()
        {
            if (!File.Exists("config.json"))
            {
                string json = JsonConvert.SerializeObject(new Config
                {
                    apiKey = "",
                    apiSecret = "",
                    quantity = 0,
                    takeProfitRate = (decimal)2.0,
                    stopLossRate = (decimal)0.8
                });
                File.WriteAllText("config.json",json);
                Utilities.Write(ConsoleColor.Red, "config.json was missing and has been created. Please edit the file and restart the application.");
                return null;
            }
            
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
        }

        private static void ExecuteOrder(string symbol, decimal quantity, decimal takeProfitRate, decimal stopLossRate)
        {
            using (var client = new BinanceClient())
            {
                string pair = symbol.ToUpper() + "BTC";
                WebCallResult<BinanceBookPrice> priceResult = client.Spot.Market.GetBookPrice(pair);
                if (priceResult.Success)
                {
                    Utilities.Write(ConsoleColor.Green, $"Price for {pair} is {priceResult.Data.BestAskPrice}");

                    //Place Market Order
                    WebCallResult<BinancePlacedOrder> order = client.Spot.Order.PlaceOrder(pair, OrderSide.Buy, OrderType.Market, null, quantity);
                    if (!order.Success)
                    {
                        Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Market order. Error code: " + order.Error?.Message);
                        return;
                    }

                    Utilities.Write(ConsoleColor.Green, $"Order submitted, Got: {order.Data.Quantity} coins from {pair} at {order.Data.Price}");

                    //Place StopLoss Order
                    decimal sellPrice = order.Data.Price * stopLossRate;
                    order = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.StopLoss, null, null, null, null, null, sellPrice);
                    if (!order.Success)
                    {
                        Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Stop Loss order. Error code: " + order.Error?.Message);
                        return;
                    }

                    //Place LimitMaker Order
                    decimal limit = order.Data.Price * takeProfitRate;
                    order = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.LimitMaker, null, null, null, limit);
                    if (!order.Success)
                    {
                        Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Limit Maker order. Error code: " + order.Error?.Message);
                        // ReSharper disable once RedundantJumpStatement 'just in case I forget'
                        return;
                    }
                }
                else
                {
                    Utilities.Write(ConsoleColor.Red, $"ERROR! Could not get price for pair: {pair}. Error code: {priceResult.Error?.Message}");
                }
            }
        }
    }
}