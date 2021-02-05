namespace OpenCryptShot
{
    public class Config
    {
        public string apiKey;
        public string apiSecret;
        public decimal quantity; // the quantity of BTC to use
        public decimal takeProfitRate; // the target profit (1.0 is no profit, 2.0 is 100% profit)
        public decimal limitPriceRate; // trigger stopLoss rate (0.8 is 20% loss)
        public decimal stopLossRate; // stop loss rate (0.75 is 25% loss)
    }
}