###上传MimeType判定测试


####测试结果

|文件名|文件类型|
|--------|--------|
|a1.mp3	| audio/mpeg |
|a2.wav	| audio/x-wav | 
|d1.docx |	application/vnd.openxmlformats-officedocument.wordprocessingml.document |
|d2.doc	| application/msword |
| d3.pptx | application/vnd.openxmlformats-officedocument.presentationml.presentation |
| d4.xlsx	| application/vnd.openxmlformats-officedocument.spreadsheetml.sheet |
| i1.jpg | image/jpeg |
| i2.bmp | image/bmp |
| i3.png | image/png |
| i4.gif | image/gif |
| i5.tif | image/tiff |
| t1.txt | text/plain |
| t2.yaml | text/yaml |
| t3.xml | application/xml |
| t4.htm | text/html |
| t5.html | text/html |
| v1.m4v | video/x-m4v |
| v2.flv | video/x-flv |
| v3.webm | video/webm |
| v4.ts | video/MP2T |
| v5.mkv | video/x-matroska |
| v6.rm | application/vnd.rn-realmedia |
| v7.avi | video/x-msvideo |
| v8.wmv | video/x-ms-wmv |
| v9.mov | video/quicktime |
| x1.exe | application/x-msdownload |
| x2_ubuntu	| application/x-executable |
| z1.rar| application/x-rar-compressed |
| z2.zip| application/zip |

####结论

一般情况下都能正确判断文件类型。

在上传过程中，虽然设置ContentType为application/octect-stream但不影响最终判别。

具体结论：

1.如果保存文件名(key)中提供了文件扩展名，那么就按照文件扩展名进行判断

1.1.如果扩展名正确，那么最终结果也正确
1.2.如果扩展名更改为其他(比如.jpg-->.bmp)会造成误判
1.3.如果扩展名非法(如.what)则等同于未提供扩展名

2.如果未提供扩展名，默认启用DetectMime(除非用户设置为关闭)并根据文件内容尝试判断

2.1.如果判断成功，就以此作为最终保存类型
2.2.如果无法判断就指定为application/octect-stream
2.3.对于image/* 如果开启dettectMime，那么结果总是正确的(已验证)
