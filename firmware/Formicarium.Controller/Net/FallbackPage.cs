namespace Formicarium.Controller.Net
{
    /// <summary>
    /// The page the ESP32 serves itself.
    ///
    /// Deliberately tiny. The Blazor dashboard is the real interface; this exists for the case
    /// where the dashboard is not running and someone needs to see whether the colony is warm —
    /// so it has no dependencies, no external requests, and fits comfortably in flash.
    /// </summary>
    public static class FallbackPage
    {
        public const string Html =
            "<!doctype html><meta name=viewport content='width=device-width,initial-scale=1'>" +
            "<title>Formicarium</title>" +
            "<style>" +
            "body{font:14px system-ui,sans-serif;margin:0;padding:1.5rem;background:#14110f;color:#e8e2d9}" +
            "h1{font-size:1.1rem;letter-spacing:.08em;text-transform:uppercase;color:#c9a227;margin:0 0 1rem}" +
            "table{border-collapse:collapse;width:100%;max-width:32rem}" +
            "td{padding:.35rem .5rem;border-bottom:1px solid #2a2520}" +
            "td:last-child{text-align:right;font-variant-numeric:tabular-nums}" +
            ".bad{color:#d4614a}.on{color:#7fb069}.off{color:#6b625a}" +
            "button{font:inherit;background:#2a2520;color:#e8e2d9;border:1px solid #3d3630;" +
            "padding:.4rem .8rem;border-radius:.25rem;margin:.25rem .25rem 0 0;cursor:pointer}" +
            "</style>" +
            "<h1>Formicarium</h1><table id=t></table>" +
            "<div><button onclick=\"post('/api/feed')\">Feed now</button>" +
            "<button onclick=\"post('/api/lighting?mode=0')\">Lights off</button>" +
            "<button onclick=\"post('/api/lighting?mode=2')\">Closed loop</button></div>" +
            "<script>" +
            "function post(u){fetch(u,{method:'POST'}).then(load)}" +
            "function row(k,v,c){return '<tr><td>'+k+'</td><td class=\"'+(c||'')+'\">'+v+'</td></tr>'}" +
            "function t(v,ok){return ok?(v.toFixed(1)+' \\u00b0C'):'--'}" +
            "function load(){fetch('/api/state').then(r=>r.json()).then(s=>{" +
            "var h='';" +
            "h+=row('Nest bottom',t(s.NestBottomTempC,s.NestBottomTempOk),s.NestBottomTempOk?'':'bad');" +
            "h+=row('Nest top',t(s.NestTopTempC,s.NestTopTempOk),s.NestTopTempOk?'':'bad');" +
            "h+=row('Outworld',t(s.OutworldTempC,s.OutworldTempOk),s.OutworldTempOk?'':'bad');" +
            "h+=row('Nest RH',s.NestHumidityOk?s.NestHumidityPct.toFixed(0)+' %':'--',s.NestHumidityOk?'':'bad');" +
            "h+=row('Soil',s.SoilMoistureOk?s.SoilMoisturePct.toFixed(0)+' %':'--',s.SoilMoistureOk?'':'bad');" +
            "h+=row('Heater',s.HeaterOn?'on':'off',s.HeaterOn?'on':'off');" +
            "h+=row('Refill pump',s.RefillPumpOn?'on':'off',s.RefillPumpOn?'on':'off');" +
            "h+=row('Reservoir',s.ReservoirFull?'full':'low',s.ReservoirFull?'on':'off');" +
            "h+=row('Fan',s.FanOn?'on':'off',s.FanOn?'on':'off');" +
            "h+=row('Riser traffic',s.RiserRatesPerMinute.map(r=>r.toFixed(1)).join(' / '));" +
            "h+=row('Channels (R,G,B)',s.RiserChannel.join(' / '));" +
            "h+=row('Faults',s.Faults,s.Faults?'bad':'');" +
            "document.getElementById('t').innerHTML=h})}" +
            "load();setInterval(load,2000);" +
            "</script>";
    }
}
