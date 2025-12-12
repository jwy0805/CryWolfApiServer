using System.ComponentModel.DataAnnotations;

namespace ApiServer.DB;

#pragma warning disable CS8618 // 생성자를 종료할 때 null을 허용하지 않는 필드에 null이 아닌 값을 포함해야 합니다. null 허용으로 선언해 보세요.

public class TestRequired
{
    public int UnitId { get; set; }
}

public class TestResponse
{
    public bool TestOk { get; set; }
    public string UnitName { get; set; }
}

public class ServerTestRequired
{
    public bool Test { get; set; }
}

public class ServerTestResponse
{
    public bool MatchTestOk { get; set; }
    public bool SocketTestOk { get; set; }
}

public class TestApiToMatchRequired
{
    public bool Test { get; set; }
}

public class TestApiToMatchResponse
{
    public bool TestOk { get; set; }
}

public class TestApiToSocketRequired
{
    public bool Test { get; set; }
}

public class TestApiToSocketResponse
{
    public bool TestOk { get; set; }
}

public class TestInitProductPacketRequired
{
    public int Any { get; set; }
}

public class TestInitProductPacketResponse
{
    public bool GetProductOk { get; set; }
    public List<ProductInfo> SpecialPackages { get; set; }
    public List<ProductInfo> BeginnerPackages { get; set; }
    public List<ProductInfo> GoldPackages { get; set; }
    public List<ProductInfo> SpinelPackages { get; set; }
    public List<ProductInfo> GoldItems { get; set; }
    public List<ProductInfo> SpinelItems { get; set; }
    public List<ProductInfo> ReservedSales { get; set; }
    public List<DailyProductInfo> DailyProducts { get; set; }
    public ProductInfo AdsRemover { get; set; }
    public DateTime RefreshTime { get; set; }
}

public class TestMapProductInfoPacketRequired
{
    public int ProductId { get; set; }
    public ProductType ProductType { get; set; }
}

public class TestMapProductInfoPacketResponse
{
    public ProductInfo ProductInfo { get; set; }
}

public class TestUnpackProductsPacketRequired
{
    public Dictionary<int, int> MailIdProductId { get; set; }
}

public class TestUnpackProductsPacketResponse
{
    public bool UnpackOk { get; set; }
}

public class TestClassifyProductPacketRequired
{
    public List<int> ProductIds { get; set; }
}

public class TestClassifyProductPacketResponse
{
    public List<ProductComposition> Compositions { get; set; }
}

public class TestDrawRandomProductPacketRequired
{
    public List<int> ProductIds { get; set; }
}

public class TestDrawRandomProductPacketResponse
{
    public List<CompositionInfo> CompositionInfos { get; set; }
}

public class TestClaimAllPacketRequired
{
    public AcquisitionPath AcquisitionPath { get; set; }
    public RewardPopupType CurrentState { get; set; }
}