using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PW_ZAI_311.Entieties
{
    #region AgregatRoot
    public abstract class AgregatRoot
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        // Mapowane na "createdAt" pod schemat Sensor w zai-api-26z.yaml (UTC zapewnia sufiks 'Z' dla ISO 8601)
        [JsonPropertyName("createdAt")]
        public DateTime CreationDataTime { get; set; }

        public AgregatRoot()
        {
            Id = Guid.NewGuid();
            CreationDataTime = DateTime.UtcNow;
        }
    }
    #endregion

    #region Sensor
    public class Sensor : AgregatRoot
    {
        // Zachowano Twoją nazwę Nuame, ale w JSON zwraca "name" zgodnie z kontraktem YAML
        [JsonPropertyName("name")]
        public string Nuame { get; set; } = string.Empty;

        // Alias pomocniczy, aby w kodzie C# można było pisać zarówno sensor.Nuame, jak i sensor.Name
        [NotMapped]
        [BsonIgnore]
        [JsonIgnore]
        public string Name
        {
            get => Nuame;
            set => Nuame = value;
        }

        [JsonIgnore]
        public uint MeasurementsNumber { get; private set; }

        // W JSON zwraca "seriesId" zgodnie z zai-api-26z.yaml
        [JsonPropertyName("seriesId")]
        public Guid MeasurementSerieId { get; set; }

        [NotMapped]
        [BsonIgnore]
        [JsonIgnore]
        public MeasurementSerie? MeasurementSerie { get; set; }

        // Wymagane przez T5.5 oraz A4: W bazie przechowujemy wyłącznie skrót SHA-256 klucza API i NIGDY go nie zwracamy w JSON
        [JsonIgnore]
        public string ApiKeyHash { get; set; } = string.Empty;

        // Opcjonalne pole ze schematu Sensor w zai-api-26z.yaml
        [JsonPropertyName("lastMeasurementAt")]
        public DateTime? LastMeasurementAt { get; set; }

        public Sensor() : base() { }

        public Sensor(string nuame, MeasurementSerie measurementSerie) : this()
        {
            Nuame = nuame;
            MeasurementsNumber = 0;
            MeasurementSerie = measurementSerie;
            if (measurementSerie != null)
            {
                MeasurementSerieId = measurementSerie.Id;
            }
        }

        // Dodatkowy konstruktor przyjmujący bezpośrednio ID serii oraz hash klucza API
        public Sensor(string nuame, Guid measurementSerieId, string apiKeyHash = "") : this()
        {
            Nuame = nuame;
            MeasurementsNumber = 0;
            MeasurementSerieId = measurementSerieId;
            ApiKeyHash = apiKeyHash;
        }

        public void RegisterNewMeasurement(DateTime measurementUtcTime)
        {
            MeasurementsNumber++;
            if (!LastMeasurementAt.HasValue || measurementUtcTime > LastMeasurementAt.Value)
            {
                LastMeasurementAt = DateTime.SpecifyKind(measurementUtcTime, DateTimeKind.Utc);
            }
        }

        public List<Measurement> AddMeasurements(List<(Sensor sensor, float value, DateTime dateTime, MeasurementSerie measurementSerie)> measurements)
        {
            var list = new List<Measurement>();
            var groupId = Guid.NewGuid();
            foreach (var item in measurements)
            {
                list.Add(new Measurement(this, item.value, item.dateTime, groupId, item.measurementSerie));
            }

            return list;
        }

        /// <summary>
        /// Generuje kryptograficznie losowy klucz API o długości 48 znaków (Wymaganie T5.5: min. 32 znaki)
        /// </summary>
        public static string GeneratePlainApiKey()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        }

        /// <summary>
        /// Oblicza skrót SHA-256 z klucza API czujnika do zapisu i weryfikacji w bazie danych (Wymaganie T5.5)
        /// </summary>
        public static string HashApiKey(string plainApiKey)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainApiKey));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
    #endregion

    #region Measurement
    public class Measurement : AgregatRoot
    {
        [JsonPropertyName("sensorId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? SensorId { get; set; }

        [NotMapped]
        [BsonIgnore]
        [JsonIgnore]
        public Sensor? Sensor { get; set; }

        [JsonPropertyName("value")]
        public float Value { get; set; }

        private DateTime _dateTime;

        // W JSON zwraca "timestamp" w formacie ISO 8601 UTC zgodnie z zai-api-26z.yaml
        [JsonPropertyName("timestamp")]
        public DateTime DateTime
        {
            get => _dateTime;
            set => _dateTime = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
        }

        [JsonIgnore]
        public Guid GroupId { get; set; }

        // W JSON zwraca "seriesId" zgodnie z zai-api-26z.yaml
        [JsonPropertyName("seriesId")]
        public Guid MeasurementSerieId { get; set; }

        [NotMapped]
        [BsonIgnore]
        [JsonIgnore]
        public MeasurementSerie? MeasurementSerie { get; set; }

        public Measurement() : base()
        {
            DateTime = DateTime.UtcNow;
        }

        public Measurement(Sensor sensor, float value, DateTime dateTime) : this()
        {
            Sensor = sensor;
            if (sensor != null)
            {
                SensorId = sensor.Id;
                MeasurementSerieId = sensor.MeasurementSerieId;
            }
            Value = value;
            DateTime = dateTime;
            GroupId = Guid.NewGuid();
        }

        public Measurement(Sensor sensor, float value, DateTime dateTime, Guid groupId, MeasurementSerie measurementSerie)
            : this(sensor, value, dateTime)
        {
            GroupId = groupId;
            MeasurementSerie = measurementSerie;
            if (measurementSerie != null)
            {
                MeasurementSerieId = measurementSerie.Id;
            }
        }

        // Wygodny konstruktor do użycia w CreateMeasurementCommandHandler
        public Measurement(Guid? sensorId, Guid measurementSerieId, float value, DateTime dateTime) : this()
        {
            SensorId = sensorId;
            MeasurementSerieId = measurementSerieId;
            Value = value;
            DateTime = dateTime;
            GroupId = Guid.NewGuid();
        }
    }
    #endregion

    #region MeasurementSerie
    public class MeasurementSerie : AgregatRoot
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("maxValue")]
        public float MaxValue { get; set; }

        [JsonPropertyName("minValue")]
        public float MinValue { get; set; }

        // Kolor w formacie HEX np. "#1f77b4"
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#1f77b4";

        // Zachowano Twoją nazwę IconInfo, ale w JSON mapuje się na "icon" zgodnie z zai-api-26z.yaml
        [JsonPropertyName("icon")]
        public string? IconInfo { get; set; }

        // Dodatkowe opcjonalne pole ze schematu SeriesInput w zai-api-26z.yaml
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        public MeasurementSerie() : base() { }

        public MeasurementSerie(string name, float maxValue, float minValue, string color, string? iconInfo = null, string? unit = null) : this()
        {
            Name = name;
            MaxValue = maxValue;
            MinValue = minValue;
            Color = color;
            IconInfo = iconInfo;
            Unit = unit;
        }

        public void Edit(string name, float maxValue, float minValue, string color, string? iconInfo, string? unit = null)
        {
            Name = name;
            MaxValue = maxValue;
            MinValue = minValue;
            Color = color;
            IconInfo = iconInfo;
            if (unit != null)
            {
                Unit = unit;
            }
        }
    }
    #endregion

    #region User
    public class User : AgregatRoot
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        // Nigdy nie serializujemy skrótu hasła w odpowiedziach API (Wymaganie A4)
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = "Admin";

        public User() : base() { }
    }
    #endregion

    #region Log
    public class Log : AgregatRoot
    {
        public uint HttpValue { get; set; }
        public bool IsError { get; set; }
        public string ActionInfo { get; set; } = string.Empty;
        public string EntityInfo { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;

        public Log() : base() { }

        private Log(uint httpValue, bool isError, string actionInfo, string entityInfo, string errorCode) : this()
        {
            HttpValue = httpValue;
            IsError = isError;
            ActionInfo = actionInfo;
            EntityInfo = entityInfo;
            ErrorCode = errorCode;
        }

        public static Log CreateForError<TEntity, TCommand, TError>(
            TEntity entity,
            TCommand command,
            TError errorObject,
            string errorString,
            int httpStatusCode,
            bool isError = true)
        {
            string serializedEntity = entity != null
                ? JsonSerializer.Serialize(entity)
                : string.Empty;

            string serializedCommand = command != null
                ? JsonSerializer.Serialize(command)
                : string.Empty;

            string serializedError = errorObject != null
                ? JsonSerializer.Serialize(errorObject)
                : string.Empty;

            return new Log(
                httpValue: (uint)httpStatusCode,
                isError: isError,
                actionInfo: $"Command: {typeof(TCommand).Name} | Data: {serializedCommand}",
                entityInfo: $"Entity: {typeof(TEntity).Name} | Data: {serializedEntity}",
                errorCode: $"ErrorType: {typeof(TError).Name} | Message: {errorString} | ErrorDetails: {serializedError}"
            );
        }
    }
    #endregion

    #region Outputs / Helpers (np. ColorOutput używany w SystemController)
    public class ColorOutput
    {
        public string Name { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public ColorOutput() { }

        public ColorOutput(KnownColor knownColor)
        {
            var color = Color.FromKnownColor(knownColor);
            Name = color.Name;
            A = color.A;
            R = color.R;
            G = color.G;
            B = color.B;
            Hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
    #endregion
}