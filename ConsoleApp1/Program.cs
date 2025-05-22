// See https://aka.ms/new-console-template for more information
using MemoryPack;

Console.WriteLine("Hello, World!");

var type = typeof(TestRecord);
if (type.GetProperties().Length > 0)
{
    byte[] bytes = MemoryPackSerializer.Serialize(new TestRecord());
  
    
}
else
{
    var d = MemoryPackSerializer.Deserialize<TestRecordResult>(new byte[0]);
}



public record TestRecord();
public record TestRecordResult();
