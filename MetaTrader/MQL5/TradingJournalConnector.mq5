// مسیر فایل: MetaTrader/MQL5/TradingJournalConnector.mq5
// ابتدای کد
//+------------------------------------------------------------------+
//|                                    TradingJournalConnector.mq5  |
//|                                       Copyright 2024, Developer |
//|                                                                  |
//+------------------------------------------------------------------+
#property copyright "Copyright 2024"
#property link      ""
#property version   "1.00"
#property strict

// Input parameters
input string   ServerURL = "http://localhost:5000/api/trades";  // Server URL
input string   ApiKey = "your-api-key";                          // API Key
input bool     AutoCapture = true;                               // Auto capture trades
input bool     CaptureScreenshot = true;                         // Capture screenshots
input int      CheckInterval = 1000;                             // Check interval (ms)

// Global variables
int lastPositionTotal = 0;
datetime lastCheckTime = 0;

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
    Print("TradingJournal Connector started");
    
    // Check connection
    if(!CheckServerConnection())
    {
        Print("Failed to connect to server");
        return INIT_FAILED;
    }
    
    // Set timer for periodic checks
    EventSetMillisecondTimer(CheckInterval);
    
    return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    EventKillTimer();
    Print("TradingJournal Connector stopped");
}

//+------------------------------------------------------------------+
//| Timer function                                                   |
//+------------------------------------------------------------------+
void OnTimer()
{
    if(!AutoCapture) return;
    
    CheckPositions();
}

//+------------------------------------------------------------------+
//| Check positions and send updates                                 |
//+------------------------------------------------------------------+
void CheckPositions()
{
    int currentPositionTotal = PositionsTotal();
    
    // Check for new positions
    if(currentPositionTotal != lastPositionTotal)
    {
        for(int i = 0; i < currentPositionTotal; i++)
        {
            ulong ticket = PositionGetTicket(i);
            if(ticket > 0)
            {
                if(PositionSelectByTicket(ticket))
                {
                    SendPositionData(ticket);
                }
            }
        }
        
        lastPositionTotal = currentPositionTotal;
    }
    
    // Check history for closed positions
    CheckHistory();
}

//+------------------------------------------------------------------+
//| Check history for closed positions                               |
//+------------------------------------------------------------------+
void CheckHistory()
{
    datetime currentTime = TimeCurrent();
    
    // Get history from last check
    if(HistorySelect(lastCheckTime, currentTime))
    {
        int dealsTotal = HistoryDealsTotal();
        
        for(int i = 0; i < dealsTotal; i++)
        {
            ulong ticket = HistoryDealGetTicket(i);
            
            if(ticket > 0)
            {
                ENUM_DEAL_ENTRY entry = (ENUM_DEAL_ENTRY)HistoryDealGetInteger(ticket, DEAL_ENTRY);
                
                // Only process OUT deals (position close)
                if(entry == DEAL_ENTRY_OUT || entry == DEAL_ENTRY_OUT_BY)
                {
                    SendHistoryDealData(ticket);
                }
            }
        }
    }
    
    lastCheckTime = currentTime;
}

//+------------------------------------------------------------------+
//| Send position data to server                                     |
//+------------------------------------------------------------------+
void SendPositionData(ulong ticket)
{
    string json = BuildPositionJSON(ticket, false);
    
    if(CaptureScreenshot)
    {
        string screenshotPath = CaptureChart(ticket);
        json = AddScreenshotToJSON(json, screenshotPath);
    }
    
    SendToServer(json);
}

//+------------------------------------------------------------------+
//| Send closed deal data to server                                  |
//+------------------------------------------------------------------+
void SendHistoryDealData(ulong ticket)
{
    string json = BuildHistoryJSON(ticket);
    
    if(CaptureScreenshot)
    {
        string screenshotPath = CaptureChart(ticket);
        json = AddScreenshotToJSON(json, screenshotPath);
    }
    
    SendToServer(json);
}

//+------------------------------------------------------------------+
//| Build JSON for position                                          |
//+------------------------------------------------------------------+
string BuildPositionJSON(ulong ticket, bool isClosed)
{
    string json = "{";
    
    json += "\"ticket\":" + IntegerToString(ticket) + ",";
    json += "\"symbol\":\"" + PositionGetString(POSITION_SYMBOL) + "\",";
    json += "\"type\":\"" + GetPositionType(PositionGetInteger(POSITION_TYPE)) + "\",";
    json += "\"volume\":" + DoubleToString(PositionGetDouble(POSITION_VOLUME), 2) + ",";
    json += "\"entryPrice\":" + DoubleToString(PositionGetDouble(POSITION_PRICE_OPEN), 5) + ",";
    json += "\"entryTime\":\"" + TimeToString(PositionGetInteger(POSITION_TIME), TIME_DATE|TIME_SECONDS) + "\",";
    json += "\"stopLoss\":" + DoubleToString(PositionGetDouble(POSITION_SL), 5) + ",";
    json += "\"takeProfit\":" + DoubleToString(PositionGetDouble(POSITION_TP), 5) + ",";
    json += "\"currentPrice\":" + DoubleToString(PositionGetDouble(POSITION_PRICE_CURRENT), 5) + ",";
    json += "\"profit\":" + DoubleToString(PositionGetDouble(POSITION_PROFIT), 2) + ",";
    json += "\"swap\":" + DoubleToString(PositionGetDouble(POSITION_SWAP), 2) + ",";
    json += "\"commission\":" + DoubleToString(PositionGetDouble(POSITION_COMMISSION), 2) + ",";
    json += "\"magic\":" + IntegerToString(PositionGetInteger(POSITION_MAGIC)) + ",";
    json += "\"comment\":\"" + PositionGetString(POSITION_COMMENT) + "\",";
    json += "\"platform\":\"MetaTrader5\",";
    json += "\"accountNumber\":\"" + IntegerToString(AccountInfoInteger(ACCOUNT_LOGIN)) + "\",";
    json += "\"isClosed\":" + (isClosed ? "true" : "false");
    
    json += "}";
    
    return json;
}

//+------------------------------------------------------------------+
//| Build JSON for history deal                                      |
//+------------------------------------------------------------------+
string BuildHistoryJSON(ulong ticket)
{
    string json = "{";
    
    json += "\"ticket\":" + IntegerToString(ticket) + ",";
    json += "\"symbol\":\"" + HistoryDealGetString(ticket, DEAL_SYMBOL) + "\",";
    json += "\"type\":\"" + GetDealType(HistoryDealGetInteger(ticket, DEAL_TYPE)) + "\",";
    json += "\"volume\":" + DoubleToString(HistoryDealGetDouble(ticket, DEAL_VOLUME), 2) + ",";
    json += "\"entryPrice\":" + DoubleToString(HistoryDealGetDouble(ticket, DEAL_PRICE), 5) + ",";
    json += "\"entryTime\":\"" + TimeToString(HistoryDealGetInteger(ticket, DEAL_TIME), TIME_DATE|TIME_SECONDS) + "\",";
    json += "\"profit\":" + DoubleToString(HistoryDealGetDouble(ticket, DEAL_PROFIT), 2) + ",";
    json += "\"swap\":" + DoubleToString(HistoryDealGetDouble(ticket, DEAL_SWAP), 2) + ",";
    json += "\"commission\":" + DoubleToString(HistoryDealGetDouble(ticket, DEAL_COMMISSION), 2) + ",";
    json += "\"magic\":" + IntegerToString(HistoryDealGetInteger(ticket, DEAL_MAGIC)) + ",";
    json += "\"comment\":\"" + HistoryDealGetString(ticket, DEAL_COMMENT) + "\",";
    json += "\"platform\":\"MetaTrader5\",";
    json += "\"accountNumber\":\"" + IntegerToString(AccountInfoInteger(ACCOUNT_LOGIN)) + "\",";
    json += "\"isClosed\":true";
    
    json += "}";
    
    return json;
}

//+------------------------------------------------------------------+
//| Capture chart screenshot                                         |
//+------------------------------------------------------------------+
string CaptureChart(ulong ticket)
{
    string filename = "trade_" + IntegerToString(ticket) + "_" + 
                     IntegerToString(GetTickCount()) + ".png";
    
    string fullPath = TerminalInfoString(TERMINAL_DATA_PATH) + "\\MQL5\\Files\\" + filename;
    
    if(ChartScreenShot(0, filename, 1920, 1080))
    {
        return fullPath;
    }
    
    return "";
}

//+------------------------------------------------------------------+
//| Add screenshot path to JSON                                      |
//+------------------------------------------------------------------+
string AddScreenshotToJSON(string json, string screenshotPath)
{
    if(screenshotPath == "") return json;
    
    // Remove last "}"
    int lastBrace = StringFind(json, "}", StringLen(json) - 2);
    string newJson = StringSubstr(json, 0, lastBrace);
    
    newJson += ",\"screenshot\":\"" + screenshotPath + "\"}";
    
    return newJson;
}

//+------------------------------------------------------------------+
//| Send data to server                                              |
//+------------------------------------------------------------------+
bool SendToServer(string json)
{
    char postData[];
    char resultData[];
    string resultHeaders;
    
    ArrayResize(postData, StringToCharArray(json, postData) - 1);
    
    string headers = "Content-Type: application/json\r\n";
    headers += "Authorization: Bearer " + ApiKey + "\r\n";
    
    int timeout = 5000;
    
    int result = WebRequest(
        "POST",
        ServerURL,
        headers,
        timeout,
        postData,
        resultData,
        resultHeaders
    );
    
    if(result == 200 || result == 201)
    {
        Print("Trade data sent successfully");
        return true;
    }
    else
    {
        Print("Failed to send trade data. HTTP Code: ", result);
        return false;
    }
}

//+------------------------------------------------------------------+
//| Check server connection                                          |
//+------------------------------------------------------------------+
bool CheckServerConnection()
{
    char postData[];
    char resultData[];
    string resultHeaders;
    
    string testJson = "{\"test\":true}";
    ArrayResize(postData, StringToCharArray(testJson, postData) - 1);
    
    string headers = "Content-Type: application/json\r\n";
    headers += "Authorization: Bearer " + ApiKey + "\r\n";
    
    int result = WebRequest(
        "GET",
        ServerURL + "/ping",
        headers,
        5000,
        postData,
        resultData,
        resultHeaders
    );
    
    return (result == 200);
}

//+------------------------------------------------------------------+
//| Get position type string                                         |
//+------------------------------------------------------------------+
string GetPositionType(long type)
{
    if(type == POSITION_TYPE_BUY) return "Buy";
    if(type == POSITION_TYPE_SELL) return "Sell";
    return "Unknown";
}

//+------------------------------------------------------------------+
//| Get deal type string                                             |
//+------------------------------------------------------------------+
string GetDealType(long type)
{
    if(type == DEAL_TYPE_BUY) return "Buy";
    if(type == DEAL_TYPE_SELL) return "Sell";
    return "Unknown";
}
//+------------------------------------------------------------------+
// پایان کد