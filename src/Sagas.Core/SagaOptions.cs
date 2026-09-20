using System;
namespace ValheimSagas;
public sealed class SagaOptions { public string ServerName {get;set;} = "Valheim fellowship"; public string ServerAddress {get;set;} = "";
 public string DataDirectory {get;set;} = "sagas-data";
 public string WebDirectory {get;set;} = "web";
 public string ListenPrefix {get;set;} = "http://127.0.0.1:9847/";
 public bool RequireViewerToken {get;set;} = false;
 public string ViewerToken {get;set;} = "";
 public string World {get;set;} = ""; public string WorldName {get;set;} = "";
 public bool LoreEnabled {get;set;} = false;
 public string OpenRouterKey {get;set;} = "";
 public string LoreModel {get;set;} = "openrouter/free";
 public bool LoreAllowPaid {get;set;} = false; public decimal LoreMaxPrice {get;set;} = 0;
 public int LoreDailyBudget {get;set;} = 20;
 public int LoreCooldownMinutes {get;set;} = 180;
 public int LoreMilestoneEvents {get;set;} = 20;
 public int RetentionDays {get;set;} = 0;
 public int StatisticsRetentionDays {get;set;} = 0;
 public int QueueCapacity {get;set;} = 8192;
 public int PresenceTimeoutSeconds {get;set;} = 20;
 public bool? SlsInstalled {get;set;}
 public bool Synthetic {get;set;} = false;
 public Action<string>? Log {get;set;}
}
