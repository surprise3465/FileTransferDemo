import flask, os,sys,time
from flask import request, send_from_directory,json

interface_path = os.path.dirname(__file__)
sys.path.insert(0, interface_path)  #将当前文件的父目录加入临时系统变量


server = flask.Flask(__name__)


#get方法：指定目录下载文件
@server.route('/download', methods=['post'])
def download():
    fpath = request.values.get('path', '') #文件路径
    print(fpath)
    fname = request.values.get('filename', '')  #文件名
    print(fname)
    if fname.strip() and fpath.strip():
        print(fname, fpath)
        if os.path.isfile(os.path.join(fpath,fname)) and os.path.isdir(fpath):
            return send_from_directory(fpath, fname, as_attachment=True) #返回文件text内容给客户端
        else:
            return '{"msg":"参数不正确"}'
    else:
        return '{"msg":"请输入参数"}'


# get方法：查询当前路径下的所有文件
@server.route('/getfiles/<fpath>', methods=['get'])
def getfiles(fpath):
    #fpath = request.values.get('fpath', '') #获取用户输入的目录
    #print(fpath)
    if os.path.isdir(fpath):
        filelist = os.listdir(fpath)
        files = [file for file in filelist if os.path.isfile(os.path.join(fpath, file))]
    return '{"files":"%s"}' % files


# post方法：上传文件的
@server.route('/upload', methods=['post'])
def upload():
    fname = request.files.get('file')  #获取上传的文件
    print(request.files)
    print(request.values)
    if fname:
        t = time.strftime('%Y%m%d%H%M%S')
        new_fname = r'upload/' + t + fname.filename
        fname.save(new_fname)  #保存文件到指定路径
        return '{"status": "ok"}'
    else:
        return '{"msg": "请上传文件！"}'



@server.route('/dosth', methods=['post'])
def dosth():
    strMsg = request.values
    print(str(request.values))
    return '{"msg": "ok"}'
    
server.run(port=8000, debug=True)


