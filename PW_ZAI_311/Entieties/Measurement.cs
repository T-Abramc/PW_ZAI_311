using System.Data;
using System.Drawing;
using System.Text.Json;

namespace PW_ZAI_311.Entieties
{
    public abstract class AgregatRoot 
    {
        public Guid Id { get; set; }
        public DateTime CreationDataTime { get; set; }

        public AgregatRoot()
        {
            Id = Guid.NewGuid();
            CreationDataTime = DateTime.Now;
        }
    }

    public class Sensor : AgregatRoot
    {
        public string Nuame { get; set; }
        public uint MeasurementsNumber { get; private set; }

        public Sensor(string nuame) : base()
        {
            Nuame = nuame;
            MeasurementsNumber = 0;
        }
        public List<Measurement> AddMeasurements(List<(Sensor sensor, float value, DateTime dateTime, MeasurementSerie measurementSerie)> measurements) 
        {
            var list = new List<Measurement>();
            var groupId = Guid.NewGuid();
            foreach (var item in measurements)
                list.Add(new Measurement(this, item.value, item.dateTime, groupId, item.measurementSerie));

            return list;
        }
    }
    #region Measurement 
    public class Measurement : AgregatRoot
    {
        public Guid? SensorId { get; set; }
        public Sensor Sensor { get; set; }
        public float Value { get; set; }
        public DateTime DateTime { get; set; }

        public Guid GroupId { get; set; }
        public Guid MeasurementSerieId { get; set; }
        public MeasurementSerie MeasurementSerie { get; set; }


        public Measurement(Sensor sensor, float value, DateTime dateTime) : base()
        {
            Sensor = sensor;
            Value = value;
            DateTime = dateTime;
            GroupId = Guid.NewGuid();   
        }
        public Measurement(Sensor sensor, float value, DateTime dateTime, Guid groupId, MeasurementSerie measurementSerie) : this(sensor, value, dateTime) 
        {
            GroupId = groupId;
            MeasurementSerie = measurementSerie;
        }

    }
    #endregion
    public class MeasurementSerie : AgregatRoot
    {
        public string Name { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
        public Color? Color { get; set; }
        public string IconInfo { get; set; }
    }
    public class Log : AgregatRoot
    {
        public uint HttpValue { get; set; }
        public bool IsError { get; set; }
        public string ActionInfo { get; set; }
        public string EntityInfo { get; set; }
        public string ErrorCode { get; set; }

        private Log(uint httpValue, bool isError, string actionInfo, string entityInfo, string errorCode): base()
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
