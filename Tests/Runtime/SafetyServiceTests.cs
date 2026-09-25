using NUnit.Framework;

namespace Taiyo.Metaverse.Tests
{
    public sealed class SafetyServiceTests
    {
        [Test]
        public void BlockingAlsoMutesPeer()
        {
            var service = new SafetyService();
            var peer = new PeerId("peer");
            service.SetBlocked(peer, true);
            Assert.That(service.IsBlocked(peer), Is.True);
            Assert.That(service.IsMuted(peer), Is.True);
        }
    }
}
