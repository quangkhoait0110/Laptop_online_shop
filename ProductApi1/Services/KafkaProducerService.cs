using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace ProductApi1.Services
{
    public class KafkaProducerService
    {
        private readonly IProducer<string, string> _producer;

        public KafkaProducerService()
        {
            var config = new ProducerConfig { BootstrapServers = "10.70.123.76:31633" };
            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task SendMessageAsync(string topic, string key, object value)
        {
            var message = new Message<string, string>
            {
                Key = key,
                Value = JsonConvert.SerializeObject(value)
            };
            await _producer.ProduceAsync(topic, message);
        }
    }
}
