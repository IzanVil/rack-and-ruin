using System;
using ServerGame.Core;

namespace ServerGame.Events
{
    public enum LogLevel { Info, Success, Warning, Critical }

    /// <summary>Una línea de la consola de eventos.</summary>
    public readonly struct LogEntry
    {
        public readonly string Message;
        public readonly LogLevel Level;
        public readonly int Day;
        public readonly float DayTime;

        public LogEntry(string message, LogLevel level, int day, float dayTime)
        {
            Message = message;
            Level = level;
            Day = day;
            DayTime = dayTime;
        }
    }

    /// <summary>Resumen que se muestra al terminar un turno.</summary>
    public readonly struct DaySummary
    {
        public readonly int Day;
        public readonly float Served;
        public readonly float Dropped;
        public readonly float Revenue;
        public readonly float Costs;
        public readonly float OperatingCost;
        public readonly float Bonus;
        public readonly float Sla;
        public readonly float Reputation;
        public readonly float MoneyAfter;

        public DaySummary(int day, float served, float dropped, float revenue, float costs,
            float operatingCost, float bonus, float sla, float reputation, float moneyAfter)
        {
            Day = day;
            Served = served;
            Dropped = dropped;
            Revenue = revenue;
            Costs = costs;
            OperatingCost = operatingCost;
            Bonus = bonus;
            Sla = sla;
            Reputation = reputation;
            MoneyAfter = moneyAfter;
        }
    }

    public readonly struct GameOverInfo
    {
        public readonly string Title;
        public readonly string Reason;
        public readonly int DaysSurvived;
        public readonly float TotalServed;
        public readonly float Money;
        public readonly int Score;
        public readonly int BestScore;
        public readonly bool IsNewRecord;

        public GameOverInfo(string title, string reason, int daysSurvived, float totalServed,
            float money, int score, int bestScore, bool isNewRecord)
        {
            Title = title;
            Reason = reason;
            DaysSurvived = daysSurvived;
            TotalServed = totalServed;
            Money = money;
            Score = score;
            BestScore = bestScore;
            IsNewRecord = isNewRecord;
        }
    }

    /// <summary>Canal de eventos de la partida. Es una instancia (no estático) a propósito:
    /// al reiniciar no quedan suscriptores colgados de la sesión anterior, que es el fallo
    /// clásico de los buses estáticos en Unity cuando se desactiva el domain reload.</summary>
    public sealed class EventBus
    {
        /// <summary>Se ha escrito una línea nueva en la consola.</summary>
        public event Action<LogEntry> Logged;

        /// <summary>Ha cambiado algo estructural (compra, avería, tarea iniciada/terminada).
        /// La interfaz lo usa para reconstruir listas; los valores numéricos se leen por
        /// sondeo en cada frame, que para esta escala es más simple y más barato.</summary>
        public event Action Changed;

        public event Action<DaySummary> DayEnded;
        public event Action<GameOverInfo> GameOver;

        /// <summary>Un servidor concreto merece atención (avería, sobrecalentamiento...).
        /// La interfaz lo usa para el destello de la tarjeta.</summary>
        public event Action<ServerUnit, LogLevel> ServerAlert;

        public void Log(string message, LogLevel level, int day, float dayTime)
        {
            Logged?.Invoke(new LogEntry(message, level, day, dayTime));
        }

        public void RaiseChanged() => Changed?.Invoke();
        public void RaiseDayEnded(DaySummary summary) => DayEnded?.Invoke(summary);
        public void RaiseGameOver(GameOverInfo info) => GameOver?.Invoke(info);
        public void RaiseServerAlert(ServerUnit unit, LogLevel level) => ServerAlert?.Invoke(unit, level);
    }
}
