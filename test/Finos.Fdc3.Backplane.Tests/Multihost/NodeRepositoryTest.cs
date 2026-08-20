/*
	* SPDX-License-Identifier: Apache-2.0
	* Copyright 2022 FINOS FDC3 contributors - see NOTICE file
	*/

using AutoFixture;
using Finos.Fdc3.Backplane.MultiHost;
using NUnit.Framework;
using System;
using System.Linq;

namespace Finos.Fdc3.Backplane.Tests.Multihost
{
    internal class NodeRepositoryTest
    {
        private Fixture _fixture;

        [SetUp]
        public void Setup()
        {
            _fixture = AutoFixtures.Create();
        }

        [Test]
        public void ShouldAddNode()
        {
            //Arrange
            Uri url = new Uri("http://url1");
            NodesRepository sut = _fixture.Create<NodesRepository>();
            //Act
            sut.AddNode(url);
            //Assert
            Assert.That(sut.MemberNodes.Count(), Is.EqualTo(1));
            Assert.That(sut.MemberNodes.ToList().First(), Is.EqualTo(url));
        }

        [Test]
        public void ShouldNotAddDuplicateNode()
        {
            //Arrange
            Uri url = new Uri("http://url1");
            NodesRepository sut = _fixture.Create<NodesRepository>();
            sut.AddNode(url);
            sut.AddNode(url);
            //Assert
            Assert.That(sut.MemberNodes.Count(), Is.EqualTo(1));
            Assert.That(sut.MemberNodes.ToList().First(), Is.EqualTo(url));
        }

        [Test]
        public void ShouldRemoveNode()
        {
            //Arrange
            Uri url = new Uri("http://url1");
            NodesRepository sut = _fixture.Create<NodesRepository>();
            //Act
            sut.AddNode(url);
            //Assert
            Assert.That(sut.MemberNodes.Count(), Is.EqualTo(1));
            Assert.That(sut.MemberNodes.ToList().First(), Is.EqualTo(url));
            sut.RemoveNode(url);
            Assert.That(sut.MemberNodes.Count(), Is.EqualTo(0));
        }

        [Test]
        public void MultipleRemoveShouldNotThrowException()
        {
            //Arrange
            Uri url = new Uri("http://url1");
            NodesRepository sut = _fixture.Create<NodesRepository>();
            //Act
            sut.AddNode(url);
            //Assert
            Assert.That(sut.MemberNodes.Count(), Is.EqualTo(1));
            Assert.That(sut.MemberNodes.ToList().First(), Is.EqualTo(url));
            sut.RemoveNode(url);
            sut.RemoveNode(url);
            sut.RemoveNode(url);
            Assert.That(sut.MemberNodes.Count(), Is.EqualTo(0));

        }


        [Test]
        public void MemberNodesShouldBeImmutable()
        {
            //Arrange
            Uri url1 = new Uri("http://url1");
            Uri url2 = new Uri("http://url2");
            Uri url_not_present = new Uri("http://notpresent");
            NodesRepository sut = _fixture.Create<NodesRepository>();
            sut.AddNode(url1);
            System.Collections.Generic.IEnumerable<Uri> ref1 = sut.MemberNodes;
            System.Collections.Generic.IEnumerable<Uri> ref2 = sut.MemberNodes;
            Assert.That(ReferenceEquals(ref2, ref1), Is.False);
        }
    }
}
