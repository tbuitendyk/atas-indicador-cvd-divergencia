//+------------------------------------------------------------------+
//| SessionsSweepsBridge.mq5                                         |
//| Puente entre Sessions Sweeps Pro (Suite) en ATAS y MetaTrader 5. |
//|                                                                  |
//| Consulta al indicador (http://127.0.0.1:<puerto>/comandos) las   |
//| órdenes pendientes y le devuelve fichas de símbolo, ejecuciones, |
//| posiciones y errores (/reporte). Las órdenes llegan con SL y TP  |
//| como DISTANCIAS: aquí se convierten al precio actual del símbolo |
//| de MT5 (nunca se copian precios del futuro).                     |
//|                                                                  |
//| Requisito de MT5: Herramientas > Opciones > Asesores Expertos >  |
//| "Permitir WebRequest para las URL" con http://127.0.0.1:5005     |
//| (el puerto elegido en el indicador). // VERIFICAR en MT5 real.   |
//| Pruebe primero en una cuenta DEMO.                               |
//+------------------------------------------------------------------+
#property copyright "Sessions Sweeps Pro"
#property version   "1.00"
#property strict

#include <Trade/Trade.mqh>

input string InpUrl      = "http://127.0.0.1:5005"; // URL del puente (la del indicador)
input int    InpPollMs   = 500;                     // Consulta cada (ms)
input long   InpMagic    = 77077;                   // Número mágico de las órdenes del puente

CTrade g_trade;

//+------------------------------------------------------------------+
int OnInit()
{
   g_trade.SetExpertMagicNumber(InpMagic);
   EventSetMillisecondTimer(MathMax(100, InpPollMs));
   return INIT_SUCCEEDED;
}

void OnDeinit(const int reason)
{
   EventKillTimer();
}

//+------------------------------------------------------------------+
//| Petición HTTP simple (texto plano).                              |
//+------------------------------------------------------------------+
bool Http(const string metodo, const string ruta, const string cuerpo, string &respuesta)
{
   char datos[], resultado[];
   string cabeceras = "Content-Type: text/plain\r\n", cabecerasRespuesta;
   StringToCharArray(cuerpo, datos, 0, StringLen(cuerpo), CP_UTF8);
   ResetLastError();
   int codigo = WebRequest(metodo, InpUrl + ruta, cabeceras, 3000, datos, resultado, cabecerasRespuesta);
   if(codigo != 200)
   {
      if(codigo == -1)
         Print("SessionsSweepsBridge: WebRequest falló (", GetLastError(), "). ¿Está la URL permitida en las opciones de MT5?");
      return false;
   }
   respuesta = CharArrayToString(resultado, 0, -1, CP_UTF8);
   return true;
}

void Reportar(const string lineas)
{
   string nada;
   if(StringLen(lineas) > 0)
      Http("POST", "/reporte", lineas, nada);
}

string D(double v, int digitos) { return DoubleToString(v, digitos); }

//+------------------------------------------------------------------+
//| Ficha del símbolo: tick, point, digits, volumen y valor del tick |
//+------------------------------------------------------------------+
string Ficha(const string simbolo)
{
   if(!SymbolSelect(simbolo, true))
      return "ERROR|0|símbolo " + simbolo + " no disponible";
   return "INFO|" + simbolo + "|" +
          D(SymbolInfoDouble(simbolo, SYMBOL_TRADE_TICK_SIZE), 10) + "|" +
          D(SymbolInfoDouble(simbolo, SYMBOL_POINT), 10) + "|" +
          IntegerToString(SymbolInfoInteger(simbolo, SYMBOL_DIGITS)) + "|" +
          D(SymbolInfoDouble(simbolo, SYMBOL_VOLUME_MIN), 8) + "|" +
          D(SymbolInfoDouble(simbolo, SYMBOL_VOLUME_MAX), 8) + "|" +
          D(SymbolInfoDouble(simbolo, SYMBOL_VOLUME_STEP), 8) + "|" +
          D(SymbolInfoDouble(simbolo, SYMBOL_TRADE_TICK_VALUE), 10);
}

//+------------------------------------------------------------------+
//| ORDEN|eventId|simbolo|BUY/SELL|lotes|distSL|distTP|slippage|nota |
//+------------------------------------------------------------------+
string Orden(string &c[])
{
   if(ArraySize(c) < 9)
      return "ERROR|0|orden incompleta";
   string id = c[1], simbolo = c[2];
   bool compra = (c[3] == "BUY");
   double lotes = StringToDouble(c[4]), distSl = StringToDouble(c[5]), distTp = StringToDouble(c[6]);
   int digitos = (int)SymbolInfoInteger(simbolo, SYMBOL_DIGITS);
   double precio = compra ? SymbolInfoDouble(simbolo, SYMBOL_ASK) : SymbolInfoDouble(simbolo, SYMBOL_BID);
   if(precio <= 0 || lotes <= 0 || distSl <= 0)
      return "ERROR|" + id + "|precio, lotes o stop no válidos";
   double sl = NormalizeDouble(compra ? precio - distSl : precio + distSl, digitos);
   double tp = distTp > 0 ? NormalizeDouble(compra ? precio + distTp : precio - distTp, digitos) : 0;
   g_trade.SetDeviationInPoints((ulong)StringToInteger(c[7]));
   bool ok = compra ? g_trade.Buy(lotes, simbolo, precio, sl, tp, c[8]) : g_trade.Sell(lotes, simbolo, precio, sl, tp, c[8]);
   if(!ok)
      return "ERROR|" + id + "|" + IntegerToString(g_trade.ResultRetcode()) + " " + g_trade.ResultRetcodeDescription();
   return "FILL|" + id + "|" + IntegerToString((long)g_trade.ResultOrder()) + "|" + D(g_trade.ResultPrice(), digitos) + "|" + D(lotes, 2);
}

string CerrarTodo()
{
   string r = "";
   for(int i = PositionsTotal() - 1; i >= 0; i--)
   {
      ulong ticket = PositionGetTicket(i);
      if(ticket > 0 && PositionGetInteger(POSITION_MAGIC) == InpMagic)
         if(!g_trade.PositionClose(ticket))
            r += "ERROR|0|no se pudo cerrar " + IntegerToString((long)ticket) + "\n";
   }
   return r;
}

//+------------------------------------------------------------------+
//| Posiciones del puente: POS|simbolo|dirección|volumen             |
//+------------------------------------------------------------------+
string Posiciones()
{
   string r = "";
   for(int i = 0; i < PositionsTotal(); i++)
   {
      ulong ticket = PositionGetTicket(i);
      if(ticket == 0 || PositionGetInteger(POSITION_MAGIC) != InpMagic)
         continue;
      int dir = PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ? 1 : -1;
      r += "POS|" + PositionGetString(POSITION_SYMBOL) + "|" + IntegerToString(dir) + "|" + D(PositionGetDouble(POSITION_VOLUME), 2) + "\n";
   }
   return r;
}

//+------------------------------------------------------------------+
void OnTimer()
{
   string texto;
   if(!Http("GET", "/comandos", "", texto))
      return;

   string lineas[], informe = "";
   int n = StringSplit(texto, '\n', lineas);
   for(int i = 0; i < n; i++)
   {
      string c[];
      if(StringSplit(lineas[i], '|', c) < 2)
         continue;
      if(c[0] == "INFO")
         informe += Ficha(c[1]) + "\n";
      else if(c[0] == "ORDEN")
         informe += Orden(c) + "\n";
      else if(c[0] == "CERRAR_TODO")
         informe += CerrarTodo();
   }

   informe += Posiciones();
   Reportar(informe);
}

//+------------------------------------------------------------------+
//| Cierre de una posición del puente: CLOSED|simbolo|resultado USD  |
//+------------------------------------------------------------------+
void OnTradeTransaction(const MqlTradeTransaction &t, const MqlTradeRequest &rq, const MqlTradeResult &rs)
{
   if(t.type != TRADE_TRANSACTION_DEAL_ADD || !HistoryDealSelect(t.deal))
      return;
   if(HistoryDealGetInteger(t.deal, DEAL_MAGIC) != InpMagic || HistoryDealGetInteger(t.deal, DEAL_ENTRY) != DEAL_ENTRY_OUT)
      return;
   double resultado = HistoryDealGetDouble(t.deal, DEAL_PROFIT) + HistoryDealGetDouble(t.deal, DEAL_SWAP) + HistoryDealGetDouble(t.deal, DEAL_COMMISSION);
   Reportar("CLOSED|" + HistoryDealGetString(t.deal, DEAL_SYMBOL) + "|" + D(resultado, 2));
}
