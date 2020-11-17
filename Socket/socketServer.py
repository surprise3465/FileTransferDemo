import socket
import threading
import os
import sys
class ThreadedServer(object):
    def __init__(self, host,port):

        self.BASE_DIR = os.path.dirname(os.path.abspath(__file__))

        self.host = host
        self.port = port
        
        self.sockServer = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sockServer.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)             
        self.sockServer.bind((self.host, self.port))        

    def listen(self):
        self.sockServer.listen(10)
        while True:
            client, address = self.sockServer.accept()
            client.settimeout(60)
            threading.Thread(target = self.listenToClient,args = (client,address)).start()

    def listenToClient(self, client, address):
        size = 1024
        while True:
            try:
                recvData = client.recv(size)
                if recvData:
                    self.MessageParser(recvData.decode('utf-8'),client, address)
                else:
                    client.close()
                    break                                     
            except:
                client.close()
                break

    def MessageParser(self, strInput, client, address):
        strList = strInput.split("|")
        if(strList[0]=="Req=1001"):
            self.RecvFilesAndSendFilesBack(strList, client, address)
        elif(strList[0]=="Req=1002"):
            self.RecvMsgAndAck(strList, client, address)
        return
        
    def RecvFilesAndSendFilesBack(self,strList,client, address):        
        InputFilePath = self.RecvFile(strList,client)
        NewFile = self.ProcessFile(InputFilePath) 
        self.SendFile(NewFile,client)    
        return  

    def RecvFile(self,strList,client):    
        buffersize = 1024
        filename = strList[1].replace("FileName=","")
        filesize = int(strList[2].replace("FileSize=",""))
        client.send("ACK=OK".encode(encoding='UTF-8'))
        filePath = os.path.join(self.BASE_DIR,filename)  #join成绝对路径，保存图片
        has_recerve = 0
        f=open(filePath,'wb')
        while has_recerve!=filesize:
            data = client.recv(buffersize)
            f.write(data)
            has_recerve += len(data)
        f.close()
        return filePath

    def ProcessFile(self, filePath):
        newFileName = filePath       
        return newFileName

    def SendFile(self, filePath, client):
        size = 1024  
        file_name = os.path.basename(filePath)
        file_size = os.stat(filePath).st_size
        file_info = "ACK=1001"+"|"+"FileName="+file_name+"|"+"FileSize="+str(file_size)
        client.send(bytes(file_info,'utf8'))
        resdata = client.recv(size)
        if(resdata):
            if(resdata.decode('utf-8')=="ACK=OK"):
                f = open(filePath,'rb')
                has_sent = 0
                while has_sent!=file_size:
                    data = f.read(size) #按指定大小从文件中读出数据
                    client.sendall(data)
                    has_sent += len(data)
                f.close() 
        return

    def RecvMsgAndAck(self,strList, client, address):        
        #do sth to local system
        #do sth to local system
        #do sth to local system
        client.send(("ACK=1002|RES=OK").encode(encoding='UTF-8'))
        return

if __name__ == "__main__":
    ipaddr = "192.168.0.102"
    port = 30041
    server = ThreadedServer(ipaddr,port)
    server.listen()