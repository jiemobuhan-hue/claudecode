namespace ZenergyBFSI.Model.MOM
{
    internal class EqptAlive_Request : BaseRequest
    {
    }
    /// <summary>
    /// 设备心跳MOM接口
    /// </summary>
    internal class EqptAlive_Response : BaseResponse
    {
        public string KeyFlag { get; set; } = "";
        //0	MOM上有材料上机的动作，需要调用材料上机的接口
        //1	MOM上有材料下机的动作，需要调用材料下机的接口
        //2	MOM上有关键零部件上机动作，需要调用关键零部件上机动作
        //3	MOM上有关键零部件下机动作，需要调用关键零部件使用和下机动作
        //4	报警信息，设备接收到MOM的信息后需要报警，不停机
        //5	停机信息，设备接收到MOM的信息后需要报警，停机
    }
}
