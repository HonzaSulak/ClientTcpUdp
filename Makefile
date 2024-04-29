# Makefile for building ClientTcpUdp.sln using dotnet build

# Variables
SOLUTION = ClientTcpUdp.sln
SRC = ClientTcpUdp
LINK = chat-client
ADDRESS = localhost
PORT = 4000

# Default target
all: clean src

# Build target
src:
	dotnet build $(SRC)
	ln -s ./$(SRC)/bin/Debug/net8.0/$(SRC) ./$(LINK)

runTCP:
	./$(LINK) -t tcp -s $(ADDRESS) -p $(PORT)

runUDP:
	./$(LINK) -t udp -s $(ADDRESS) -p $(PORT)

runHelp:
	./$(LINK) -h

# Clean target
clean:
	dotnet clean $(SOLUTION)
	rm -f $(LINK)