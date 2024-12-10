using Moq;
using NLog;
using OmronFinsNetStandard.Enums;
using OmronFinsNetStandard.Errors;
using OmronFinsNetStandard.Interfaces;
using System.Reflection;

namespace OmronFinsNetStandard.Tests
{
    public class EthernetPlcClientTests
    {
        #region ConnectAsync
        [Fact]
        public async Task ConnectAsync_ShouldReturnTrue_WhenConnectionIsSuccessful()
        {
            // Arrange
            var mockBasic = new Mock<IBasicClass>();
            var mockCommandBuilder = new Mock<IFinsCommandBuilder>();

            string plcIp = "192.168.1.10";
            int plcPort = 9600;
            int timeout = 3000;

            // Setup mock
            mockBasic.Setup(b => b.PingCheckAsync(plcIp, timeout))
                     .ReturnsAsync(true);

            // Using handshake command
            byte[] handshakeCommand = new byte[20]
            {
        0x46, 0x49, 0x4E, 0x53, // 'F', 'I', 'N', 'S'
        0x00, 0x00, // Command length
        0x00, 0x0C, // Sequence number
        0x00, 0x00, 0x00, 0x00, // Frame command
        0x00, 0x00, 0x00, 0x00, // Error code
        0x00, 0x00, 0x00, 0x00  // Command option
            };

            mockCommandBuilder.Setup(cb => cb.HandShake())
                              .Returns(handshakeCommand);

            mockBasic.Setup(b => b.SendDataAsync(handshakeCommand))
                     .Returns(Task.CompletedTask);

            // After sending handshake command
            byte[] handshakeResponse = new byte[24];
            handshakeResponse[15] = 0x00; // Error code
            handshakeResponse[19] = 0x01; // PCNode
            handshakeResponse[23] = 0x02; // PLCNode

            mockBasic.Setup(b => b.ReceiveDataAsync(It.IsAny<byte[]>()))
                     .Callback<byte[]>(buffer => Buffer.BlockCopy(handshakeResponse, 0, buffer, 0, handshakeResponse.Length))
                     .ReturnsAsync(24);

            var client = new EthernetPlcClient(mockBasic.Object, mockCommandBuilder.Object);

            // Act
            bool result = await client.ConnectAsync(plcIp, plcPort, timeout);

            // Assert
            Assert.True(result);

            // Checks calls
            mockBasic.Verify(b => b.PingCheckAsync(plcIp, timeout), Times.Once);
            mockBasic.Verify(b => b.ConnectAsync(plcIp, plcPort), Times.Once);
            mockBasic.Verify(b => b.SendDataAsync(handshakeCommand), Times.Once);
            mockBasic.Verify(b => b.ReceiveDataAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Fact]
        public async Task ConnectAsync_ShouldReturnFalse_WhenConnectionIsFailure()
        {
            // Arrange
            var mockBasic = new Mock<IBasicClass>();
            var mockCommandBuilder = new Mock<IFinsCommandBuilder>();

            string plcIp = "192.168.1.10";
            int plcPort = 9600;
            int timeout = 3000;

            // Setup mock: Ping fails
            mockBasic.Setup(b => b.PingCheckAsync(plcIp, timeout))
                     .ReturnsAsync(false);

            var client = new EthernetPlcClient(mockBasic.Object, mockCommandBuilder.Object);

            // Act
            bool result = await client.ConnectAsync(plcIp, plcPort, timeout);

            // Assert
            Assert.False(result);

            // Verify that only PingCheckAsync was called
            mockBasic.Verify(b => b.PingCheckAsync(plcIp, timeout), Times.Once);
            mockBasic.Verify(b => b.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            mockBasic.Verify(b => b.SendDataAsync(It.IsAny<byte[]>()), Times.Never);
            mockBasic.Verify(b => b.ReceiveDataAsync(It.IsAny<byte[]>()), Times.Never);
        }
        #endregion

        #region GetBitStateAsync
        [Fact]
        public async Task GetBitStateAsync_ShouldReturnBitState_WhenResponseIsValid()
        {
            // Arrange
            var mockBasic = new Mock<IBasicClass>();
            var mockCommandBuilder = new Mock<IFinsCommandBuilder>();

            PlcMemory memory = PlcMemory.DM;
            string address = "100.5";
            short expectedBitState = 1;

            // Divide address into parts
            short cnInt = 100;
            short cnBit = 5;

            // Setup FinsCmd
            byte[] expectedCommand = new byte[20]
            {
        0x46, 0x49, 0x4E, 0x53, // 'F', 'I', 'N', 'S'
        0x00, 0x1A, // Command length (Read command)
        0x00, 0x0C, // Sequence number
        0x00, 0x00, 0x00, 0x02, // Frame command
        0x00, 0x00, 0x00, 0x00, // Error code
        0x80, 0x00, 0x02, 0x00  // Command options
            };
            mockCommandBuilder.Setup(cb => cb.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Bit, cnInt, cnBit, 1))
                              .Returns(expectedCommand);

            // Setup SendDataAsync
            mockBasic.Setup(b => b.SendDataAsync(expectedCommand))
                     .Returns(Task.CompletedTask);

            // Setup ReceiveDataAsync
            byte[] responseBuffer = new byte[31];
            responseBuffer[30] = (byte)expectedBitState;
            mockBasic.Setup(b => b.ReceiveDataAsync(It.IsAny<byte[]>()))
                     .Callback<byte[]>(buffer => Buffer.BlockCopy(responseBuffer, 0, buffer, 0, responseBuffer.Length))
                     .ReturnsAsync(31);

            // Setup CheckAndThrowErrors (may not throw any exceptions)

            var client = new EthernetPlcClient(mockBasic.Object, mockCommandBuilder.Object);

            // Act
            short bitState = await client.GetBitStateAsync(memory, address);

            // Assert
            Assert.Equal(expectedBitState, bitState);

            // Checks calls
            mockCommandBuilder.Verify(cb => cb.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Bit, cnInt, cnBit, 1), Times.Once);
            mockBasic.Verify(b => b.SendDataAsync(expectedCommand), Times.Once);
            mockBasic.Verify(b => b.ReceiveDataAsync(It.IsAny<byte[]>()), Times.Once);
        }
        #endregion

        #region CheckAndThrowErrors
        [Fact]
        public void CheckAndThrowErrors_ShouldThrowFinsError_WhenHeadErrorDetected()
        {
            // Arrange
            var mockBasic = new Mock<IBasicClass>();
            var mockCommandBuilder = new Mock<IFinsCommandBuilder>();
            var client = new EthernetPlcClient(mockBasic.Object, mockCommandBuilder.Object);

            byte[] bufferWithHeadError = new byte[32];
            bufferWithHeadError[11] = (byte)HeadErrorCode.InvalidHead;
            bufferWithHeadError[12] = 0x01;
            bufferWithHeadError[28] = 0x00;
            bufferWithHeadError[29] = 0x00;

            // Act & Assert
            var exception = Assert.Throws<TargetInvocationException>(() => client.GetType()
                                                                  .GetMethod("CheckAndThrowErrors", BindingFlags.NonPublic | BindingFlags.Instance)
                                                                  .Invoke(client, new object[] { bufferWithHeadError }));

            // Checks that InnerException is FinsError
            Assert.IsType<FinsError>(exception.InnerException);

            var finsError = exception.InnerException as FinsError;
            Assert.NotNull(finsError);
            Assert.Equal((byte)HeadErrorCode.InvalidHead, finsError.MainCode);
            Assert.Equal(0x01, finsError.SubCode);
            Assert.Contains("Head Error", finsError.Description);
        }

        [Fact]
        public void CheckAndThrowErrors_ShouldThrowFinsError_WhenEndErrorDetectedAndCannotContinue()
        {
            // Arrange
            var mockBasic = new Mock<IBasicClass>();
            var mockCommandBuilder = new Mock<IFinsCommandBuilder>();
            var client = new EthernetPlcClient(mockBasic.Object, mockCommandBuilder.Object);

            byte[] bufferWithEndError = new byte[32];
            bufferWithEndError[28] = 0x02;
            bufferWithEndError[29] = 0x03;

            // Act & Assert
            var exception = Assert.Throws<TargetInvocationException>(() => client.GetType()
                                                                  .GetMethod("CheckAndThrowErrors", BindingFlags.NonPublic | BindingFlags.Instance)
                                                                  .Invoke(client, new object[] { bufferWithEndError }));

            // Checks that InnerException is FinsError
            Assert.IsType<FinsError>(exception.InnerException);

            var finsError = exception.InnerException as FinsError;
            Assert.NotNull(finsError);
            Assert.Equal(0x02, finsError.MainCode);
            Assert.Equal(0x03, finsError.SubCode);
        }

        [Fact]
        public void CheckAndThrowErrors_ShouldNotThrow_WhenNoErrorsDetected()
        {
            // Arrange
            var mockBasic = new Mock<IBasicClass>();
            var mockCommandBuilder = new Mock<IFinsCommandBuilder>();
            var client = new EthernetPlcClient(mockBasic.Object, mockCommandBuilder.Object);

            byte[] bufferWithoutErrors = new byte[32];
            // Fll codes are 0
            bufferWithoutErrors[11] = (byte)HeadErrorCode.Success;
            bufferWithoutErrors[12] = 0x00;
            bufferWithoutErrors[28] = 0x00;
            bufferWithoutErrors[29] = 0x00;

            // Act & Assert
            var exception = Record.Exception(() => client.GetType()
                                                      .GetMethod("CheckAndThrowErrors", BindingFlags.NonPublic | BindingFlags.Instance)
                                                      .Invoke(client, new object[] { bufferWithoutErrors }));

            Assert.Null(exception); 
        }
        #endregion


    }
}