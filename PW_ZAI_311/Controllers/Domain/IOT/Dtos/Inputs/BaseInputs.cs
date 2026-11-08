using System.Drawing;

namespace PW_ZAI_311.Controllers.Domain
{
    public class BaseInput
    {
        public Guid Id { get; set; }
        public BaseInput() { }
        public BaseInput(Guid id) : this()
        { Id = id; }
    }

    #region Sensor
    public class CreateSensorInput
    {
        public string Name { get; set; }
        public Guid? MeasurementSerieId { get; set; }
        public CreateSensorInput() { }

        public CreateSensorInput(string nuame, Guid? measurementSerieId)
        {
            Name = nuame;
            MeasurementSerieId = measurementSerieId;
        }
    }

    public class UpdateSensorInput : BaseInput
    {
        public string Nuame { get; set; }

        public UpdateSensorInput() : base() { }

        public UpdateSensorInput(Guid id, string nuame) : base(id)
        {
            Nuame = nuame;
        }
    }
    #endregion

    #region Measurement
    public class CreateMeasurementInput
    {
        public Guid? SensorId { get; set; }
        public float Value { get; set; }
        public DateTime DateTime { get; set; }
        public Guid GroupId { get; set; }
        public Guid MeasurementSerieId { get; set; }

        public CreateMeasurementInput() { }

        public CreateMeasurementInput(Guid? sensorId, float value, DateTime dateTime, Guid groupId, Guid measurementSerieId)
        {
            SensorId = sensorId;
            Value = value;
            DateTime = dateTime;
            GroupId = groupId;
            MeasurementSerieId = measurementSerieId;
        }
    }

    #endregion
    #region MeasurementSerie
    public class CreateMeasurementSerieInput
    {
        public string Name { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
        public KnownColor? Color { get; set; }
        public string IconInfo { get; set; }

        public CreateMeasurementSerieInput() { }

        public CreateMeasurementSerieInput(string name, float maxValue, float minValue, KnownColor? color, string iconInfo)
        {
            Name = name;
            MaxValue = maxValue;
            MinValue = minValue;
            Color = color;
            IconInfo = iconInfo;
        }
    }
    public class UpdateMeasurementSerieInput : BaseInput
    {
        public string Name { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
        public KnownColor? ThisColor { get; set; }
        public string IconInfo { get; set; }

        public UpdateMeasurementSerieInput() : base() { }

        public UpdateMeasurementSerieInput(Guid id, string name, float maxValue, float minValue, KnownColor? color, string iconInfo) : base(id)
        {
            Name = name;
            MaxValue = maxValue;
            MinValue = minValue;
            ThisColor = color;
            IconInfo = iconInfo;
        }
    }
    
    #endregion
}
