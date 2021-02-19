using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using Newtonsoft.Json;
using OpenCryptShot.Discord;
using System.Threading;

namespace OpenCryptShot
{
    internal static class Program
    {
        private static BinanceClient client;
        private static WebCallResult<BinanceExchangeInfo> exchangeInfo;
        
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

            if (config.limitPriceRate > 1)
            {
                Utilities.Write(ConsoleColor.Yellow, "Warning! limitPriceRate was over 1.0. It has been set to 1.0. This could have unwanted consequences.");
                config.limitPriceRate = 1;
            }

            try
            {
                BinanceClient.SetDefaultOptions(new BinanceClientOptions
                {
                    ApiCredentials = new ApiCredentials(config.apiKey, config.apiSecret),
                    LogVerbosity = LogVerbosity.None,
                    LogWriters = new List<TextWriter> {Console.Out},
                    TradeRulesBehaviour = TradeRulesBehaviour.AutoComply
                });
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR! Could not set Binance options. Error message: {ex.Message}");
                Console.Read();
                return;
            }

            client = new BinanceClient();
            exchangeInfo = client.Spot.System.GetExchangeInfo();
            if (!exchangeInfo.Success)
            {
                Utilities.Write(ConsoleColor.Red, $"ERROR! Could not exchange informations. Error code: " + exchangeInfo.Error?.Message);
                return;
            }
                
            Utilities.Write(ConsoleColor.Green, "Successfully logged in.");

            while (true)
            {
                //Wait for symbol input
                Utilities.Write(ConsoleColor.Yellow, "Input symbol or Discord channel ID:");
                Console.ForegroundColor = ConsoleColor.White;
                string symbol = Console.ReadLine();

                // if line is only digits, it's safe to assume it's a Discord channel ID.
                if (symbol.All(c => c >= '0' && c <= '9'))
                {
                    string channelId = symbol;
                    symbol = null;
                    Console.WriteLine("Looking for symbol...");
                    // Scrape channel every 100ms
                    while (null == symbol)
                    {
                        symbol = ScrapeChannel(config.discordToken, channelId);
                        Thread.Sleep(100);
                    }
                }

                //Exit the program if nothing was entered
                if (string.IsNullOrEmpty(symbol))
                    return;

                //Try to execute the order
                ExecuteOrder(symbol, config.quantity, config.takeProfitRate, config.stopLossRate, config.limitPriceRate);
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
                    takeProfitRate = (decimal) 2.0,
                    limitPriceRate = (decimal) 0.8,
                    stopLossRate = (decimal) 0.75,
                    discordToken = ""
                });
                File.WriteAllText("config.json", json);
                Utilities.Write(ConsoleColor.Red, "config.json was missing and has been created. Please edit the file and restart the application.");
                return null;
            }

            return JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
        }

        private static void ExecuteOrder(string symbol, decimal quantity, decimal takeProfitRate, decimal stopLossRate, decimal limitPriceRate)
        {
            string pair = symbol.ToUpper() + "BTC";
            WebCallResult<BinanceBookPrice> priceResult = client.Spot.Market.GetBookPrice(pair);
            if (priceResult.Success)
            {
                Utilities.Write(ConsoleColor.Green, $"Price for {pair} is {priceResult.Data.BestAskPrice}");
                
                BinanceSymbol symbolInfo = exchangeInfo.Data.Symbols.FirstOrDefault(s => s.QuoteAsset == "BTC" && s.BaseAsset == symbol.ToUpper());
                if (symbolInfo == null)
                {
                    Utilities.Write(ConsoleColor.Red, $"ERROR! Could not get symbol informations.");
                    return;
                }

                int symbolPrecision = symbolInfo.BaseAssetPrecision;
                Utilities.Write(ConsoleColor.Green, $"Asset precision: {symbolPrecision}");
                
                //Place Market Order
                WebCallResult<BinancePlacedOrder> order = client.Spot.Order.PlaceOrder(pair, OrderSide.Buy, OrderType.Market, null, quantity);
                if (!order.Success)
                {
                    Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Market order. Error code: " + order.Error?.Message);
                    return;
                }

                //Get the filled order average price
                decimal paidPrice = 0;
                if (order.Data.Fills != null)
                {
                    paidPrice = order.Data.Fills.Average(trade => trade.Price);
                }

                decimal orderQuantity = order.Data.QuantityFilled;

                Utilities.Write(ConsoleColor.Green, $"Order submitted, Got: {orderQuantity} coins from {pair} at {paidPrice}");

                decimal sellPrice = Math.Round(paidPrice * stopLossRate, symbolPrecision);
                decimal triggerPrice = Math.Round(paidPrice * limitPriceRate, symbolPrecision);
                decimal limit = Math.Round(paidPrice * takeProfitRate, symbolPrecision);

                WebCallResult<BinanceOrderOcoList> ocoOrder = client.Spot.Order.PlaceOcoOrder(pair, OrderSide.Sell, orderQuantity, limit, triggerPrice, sellPrice, stopLimitTimeInForce: TimeInForce.GoodTillCancel);
                if (!ocoOrder.Success)
                {
                    Utilities.Write(ConsoleColor.Red, $"OCO order failed, Error code: {ocoOrder.Error?.Message}");
                    return;
                }

                Utilities.Write(ConsoleColor.Green, $"OCO Order submitted, sell price: {limit}, stop price: {triggerPrice}, stop limit price: {sellPrice}");
            }
            else
            {
                Utilities.Write(ConsoleColor.Red, $"ERROR! Could not get price for pair: {pair}. Error code: {priceResult.Error?.Message}");
            }
        }

        /// <summary>
        /// Look for a symbol in the latest message of a given channel.
        /// </summary>
        /// <param name="discordToken">User Discord token</param>
        /// <param name="channelId">Channel ID to scrape/param>
        /// <returns>Returns the found symbol or null</returns>
        private static string ScrapeChannel(string discordToken, string channelId)
        {
            // Look for something that starts with a '$' followed by 2 to 5 alphabetic characters.
            Regex regex = new Regex(@"(\$)[a-zA-Z]{2,5}");
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://discord.com/api/v8/channels/" + channelId + "/messages?limit=1");

                req.Headers.Add("Authorization", discordToken);
                req.Accept = "*/*";
                req.ContentType = "application/json";

                HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                if (res.StatusCode == HttpStatusCode.OK)
                {

                }
                Stream dataStream = res.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string resJson = reader.ReadToEnd();
                Message[] msg = System.Text.Json.JsonSerializer.Deserialize<Message[]>(resJson);
                Match match = regex.Match(msg[0].content);
                res.Close();
                reader.Close();
                dataStream.Close();
                if (match.Success)
                {
                    // Remove '$' character
                    return match.Value.Remove(0, 1);
                }
                else
                {
                    return null;
                }
            }
            catch (WebException ex)
            {
                Utilities.Write(ConsoleColor.Red, "ERROR: Could not get Discord message. Error code: " + ex.Status);
                return null;
            }
        }
    }
}