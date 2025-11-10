// 📁 MetaTrader/MQL4/TradingJournalConnector.mq4
// ===== شروع کد =====

#property copyright "Trading Journal Platform"
#property version   "1.00"
#property strict

// تنظیمات اتصال
input string ServerUrl = "http://localhost:5000/api/trades";
input string ApiKey = "your-api-key";
input int UpdateIntervalSeconds = 5;

// متغیرهای داخلی
datetime lastUpdateTime = 0;
int webRequestTimeout = 5000;

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
    Print("Trading Journal Connector Started");
    EventSetTimer(UpdateIntervalSeconds);
    
    // ارسال معاملات موجود
    SendAllTrades();
    
    return(INIT_SUCCEEDED);
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    EventKillTimer();
    Print("Trading Journal Connector Stopped");
}

//+------------------------------------------------------------------+
//| Timer function                                                   |
//+------------------------------------------------------------------+
void OnTimer()
{
    SendAllTrades();
}

//+------------------------------------------------------------------+
//| Trade event                                                      |
//+------------------------------------------------------------------+
void OnTrade()
{
    SendAllTrades();
}

//+------------------------------------------------------------------+
//| Send all trades to server                                        |
//+------------------------------------------------------------------+
void SendAllTrades()
{
    int totalOrders = OrdersHistoryTotal();
    string jsonData = "[";
    bool hasData = false;
    
    // بررسی معاملات تاریخی
    for(int i = 0; i < totalOrders; i++)
    {
        if(OrderSelect(i, SELECT_BY_POS, MODE_HISTORY))
        {
            if(OrderType() <= OP_SELL) // فقط معاملات Buy/Sell
            {
                if(hasData) jsonData += ",";
                jsonData += FormatTradeJson();
                hasData = true;
            }
        }
    }
    
    // بررسی معاملات باز
    totalOrders = OrdersTotal();
    for(int i = 0; i < totalOrders; i++)
    {
        if(OrderSelect(i, SELECT_BY_POS, MODE_TRADES))
        {
            if(OrderType() <= OP_SELL)
            {
                if(hasData) jsonData += ",";
                jsonData += FormatTradeJson();
                hasData = true;
            }
        }
    }
    
    jsonData += "]";
    
    if(hasData)
    {
        SendToServer(jsonData);
    }
}

//+------------------------------------------------------------------+
//| Format trade as JSON                                             |
//+------------------------------------------------------------------+
string FormatTradeJson()
{
    string json = "{";
    json += "\"ticket\":" + IntegerToString(OrderTicket()) + ",";
    json += "\"symbol\":\"" + OrderSymbol() + "\",";
    json += "\"type\":\"" + (OrderType() == OP_BUY ? "BUY" : "SELL") + "\",";
    json += "\"openTime\":\"" + TimeToStr(OrderOpenTime(), TIME_DATE|TIME_SECONDS) + "\",";
    json += "\"openPrice\":" + DoubleToString(OrderOpenPrice(), Digits) + ",";
    json += "\"volume\":" + DoubleToString(OrderLots(), 2) + ",";
    json += "\"stopLoss\":" + DoubleToString(OrderStopLoss(), Digits) + ",";
    json += "\"takeProfit\":" + DoubleToString(OrderTakeProfit(), Digits) + ",";
    json += "\"commission\":" + DoubleToString(OrderCommission(), 2) + ",";
    json += "\"swap\":" + DoubleToString(OrderSwap(), 2) + ",";
    json += "\"profit\":" + DoubleToString(OrderProfit(), 2) + ",";
    json += "\"comment\":\"" + OrderComment() + "\",";
    json += "\"magicNumber\":" + IntegerToString(OrderMagicNumber()) + ",";
    
    if(OrderCloseTime() > 0)
    {
        json += "\"closeTime\":\"" + TimeToStr(OrderCloseTime(), TIME_DATE|TIME_SECONDS) + "\",";
        json += "\"closePrice\":" + DoubleToString(OrderClosePrice(), Digits) + ",";
        json += "\"status\":\"CLOSED\"";
    }
    else
    {
        json += "\"status\":\"OPEN\"";
    }
    
    json += "}";
    return json;
}

//+------------------------------------------------------------------+
//| Send data to server                                              |
//+------------------------------------------------------------------+
void SendToServer(string jsonData)
{
    char post[];
    char result[];
    string headers;
    
    headers = "Content-Type: application/json\r\n";
    headers += "X-API-Key: " + ApiKey + "\r\n";
    
    StringToCharArray(jsonData, post);
    
    int res = WebRequest(
        "POST",
        ServerUrl,
        headers,
        webRequestTimeout,
        post,
        result,
        headers
    );
    
    if(res > 0)
    {
        Print("Data sent successfully");
    }
    else
    {
        Print("Failed to send data: ", GetLastError());
    }
}

// ===== پایان کد =====