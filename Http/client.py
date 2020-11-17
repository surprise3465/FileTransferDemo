import requests
import os


#上传文件到服务器
file = {'file': open('1-P-L-D1.molo','rb')}
r = requests.post('http://127.0.0.1:8000/upload', files=file)
print(r.text)


#查询fpath下的所有文件
r1 = requests.get('http://127.0.0.1:8000/getfiles/upload')
print(r1.text)


#下载服务器download目录下的指定文件
r2 = requests.post('http://127.0.0.1:8000/download',data={'filename':'hello.txt', 'path': r'download/'})
file = r2.text #获取文件内容
basepath = os.path.join(os.path.dirname(__file__), r'download/')
with open(os.path.join(basepath, 'hello_download.txt'),'w',encoding='utf-8') as f: #保存文件
    f.write(file)