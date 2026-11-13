using EmployeeManagement.Server.Events;

namespace EmployeeManagement.Server.Interfaces;

public interface IRabbitMqPublisher
{
    // True do te thote vetem pranim ne memorien lokale, jo dorezim te RabbitMQ ose konsumim.
    bool Publish(BusinessEvent businessEvent);
}
