using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Dx.WopiServerSql.Repository.UnitTest
{
    [TestClass]
    public class FileUtilTests
    {
        [TestMethod]
        public void TestIsValidFileName()
        {
            var valid = FileNameUtil.IsValidFileName("test?");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName(".test");
            Assert.IsTrue(valid);
            valid = FileNameUtil.IsValidFileName("test.");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName("/test");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName("\\test");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName("test/");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName("test\\");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName("test\\test");
            Assert.IsFalse(valid);
            valid = FileNameUtil.IsValidFileName("test");
            Assert.IsTrue(valid);
        }

        [TestMethod]
        public void CreateValidFileName()
        {
            var validFileName = FileNameUtil.MakeValidFileName("test?");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName(".test");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("test.");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("/test");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("\test");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("test/");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("test\\");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("test\\test");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
            validFileName = FileNameUtil.MakeValidFileName("test");
            Assert.IsTrue(FileNameUtil.IsValidFileName(validFileName));
        }
    }
}
