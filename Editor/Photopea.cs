using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

/// <summary>
/// Abre Photopea para editar texturas del proyecto.
/// Unity 6 no expone un WebView público, así que Photopea se abre en el navegador.
/// Un servidor HTTP local recibe Archivo → Guardar y sobrescribe el asset original.
/// </summary>
[InitializeOnLoad]
public class PhotopeaEditorWindow : EditorWindow
{
    const string PhotopeaUrl = "https://www.photopea.com";
    const int JsonHeaderBytes = 2000;

    static readonly string[] SupportedExtensions =
    {
        ".png", ".jpg", ".jpeg", ".psd", ".psb", ".tga", ".tif", ".tiff",
        ".gif", ".bmp", ".webp", ".svg", ".iff", ".exr"
    };

    static HttpListener listener;
    static Thread listenerThread;
    static int serverPort;
    static string pendingAssetPath;
    static string pendingFullPath;
    static string pendingFormat = "png";
    static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    Label selectionLabel;
    Image preview;
    Button editButton;
    Button reimportButton;
    Label statusLabel;

    static PhotopeaEditorWindow()
    {
        EditorApplication.update += PumpMainThreadQueue;
        AssemblyReloadEvents.beforeAssemblyReload += StopSaveServer;
        EditorApplication.quitting += StopSaveServer;
    }

    [MenuItem("Tools/Photopea Editor")]
    public static void OpenWindow()
    {
        PhotopeaEditorWindow window = GetWindow<PhotopeaEditorWindow>();
        window.titleContent = new GUIContent("Photopea", EditorGUIUtility.IconContent("d_Texture2D Icon").image);
        window.minSize = new Vector2(380, 300);
        window.Show();
    }

    [MenuItem("Assets/Editar en Photopea", true)]
    static bool ValidateEditInPhotopea()
    {
        return TryGetSelectedImagePath(out _);
    }

    [MenuItem("Assets/Editar en Photopea", false, 32)]
    static void EditSelectedInPhotopea()
    {
        if (TryGetSelectedImagePath(out string assetPath))
            OpenImageInPhotopea(assetPath);
    }

    void OnEnable()
    {
        Selection.selectionChanged += RefreshSelectionUI;
    }

    void OnDisable()
    {
        Selection.selectionChanged -= RefreshSelectionUI;
    }

    void CreateGUI()
    {
        rootVisualElement.style.paddingLeft = 12;
        rootVisualElement.style.paddingRight = 12;
        rootVisualElement.style.paddingTop = 12;
        rootVisualElement.style.paddingBottom = 12;

        var title = new Label("Editor de imágenes (Photopea)");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 14;
        title.style.marginBottom = 8;
        rootVisualElement.Add(title);

        var help = new HelpBox(
            "Se abre Photopea en el navegador con la textura cargada.\n\n" +
            "Para guardar en Assets: usa Archivo → Guardar (Ctrl+S) o el botón «Guardar en Unity» de la barra superior.\n" +
            "No uses «Exportar como…» (eso solo va a Descargas).",
            HelpBoxMessageType.Info);
        help.style.marginBottom = 10;
        rootVisualElement.Add(help);

        var openEmpty = new Button(() => OpenUrl(PhotopeaUrl))
        {
            text = "Abrir Photopea vacío"
        };
        openEmpty.style.height = 24;
        openEmpty.style.marginBottom = 6;
        rootVisualElement.Add(openEmpty);

        editButton = new Button(() =>
        {
            if (TryGetSelectedImagePath(out string assetPath))
                OpenImageInPhotopea(assetPath);
            else
                EditorUtility.DisplayDialog("Photopea", "Selecciona una imagen en el Project (PNG, JPG, PSD…).", "OK");
        })
        {
            text = "Editar imagen seleccionada"
        };
        editButton.style.height = 24;
        editButton.style.marginBottom = 6;
        rootVisualElement.Add(editButton);

        reimportButton = new Button(ReimportSelection)
        {
            text = "Reimportar selección"
        };
        reimportButton.style.height = 24;
        reimportButton.style.marginBottom = 8;
        rootVisualElement.Add(reimportButton);

        selectionLabel = new Label();
        selectionLabel.style.whiteSpace = WhiteSpace.Normal;
        selectionLabel.style.marginBottom = 4;
        rootVisualElement.Add(selectionLabel);

        statusLabel = new Label();
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.color = new Color(0.65f, 0.85f, 0.65f);
        statusLabel.style.marginBottom = 8;
        rootVisualElement.Add(statusLabel);

        preview = new Image();
        preview.scaleMode = ScaleMode.ScaleToFit;
        preview.style.flexGrow = 1;
        preview.style.minHeight = 120;
        preview.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        rootVisualElement.Add(preview);

        RefreshSelectionUI();
        RefreshStatus();
    }

    void RefreshStatus()
    {
        if (statusLabel == null)
            return;

        if (listener != null && listener.IsListening && !string.IsNullOrEmpty(pendingAssetPath))
            statusLabel.text = "Servidor activo → guarda en:\n" + pendingAssetPath;
        else if (listener != null && listener.IsListening)
            statusLabel.text = "Servidor activo en 127.0.0.1:" + serverPort;
        else
            statusLabel.text = "Servidor inactivo. Abre una imagen para activarlo.";
    }

    void RefreshSelectionUI()
    {
        if (selectionLabel == null)
            return;

        bool hasImage = TryGetSelectedImagePath(out string assetPath);
        if (editButton != null)
            editButton.SetEnabled(hasImage);
        if (reimportButton != null)
            reimportButton.SetEnabled(hasImage);

        if (!hasImage)
        {
            selectionLabel.text = "Ninguna imagen seleccionada en el Project.";
            preview.image = null;
            return;
        }

        selectionLabel.text = "Selección: " + assetPath;
        preview.image = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        RefreshStatus();
    }

    static void ReimportSelection()
    {
        if (!TryGetSelectedImagePath(out string assetPath))
            return;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }

    static bool TryGetSelectedImagePath(out string assetPath)
    {
        assetPath = null;
        UnityEngine.Object obj = Selection.activeObject;
        if (obj == null)
            return false;

        assetPath = AssetDatabase.GetAssetPath(obj);
        return !string.IsNullOrEmpty(assetPath) && IsSupportedImage(assetPath);
    }

    static bool IsSupportedImage(string assetPath)
    {
        string ext = Path.GetExtension(assetPath);
        if (string.IsNullOrEmpty(ext))
            return false;

        for (int i = 0; i < SupportedExtensions.Length; i++)
        {
            if (string.Equals(ext, SupportedExtensions[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static string FormatForExtension(string assetPath)
    {
        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        switch (ext)
        {
            case ".jpg":
            case ".jpeg":
                return "jpg:0.92";
            case ".psd":
            case ".psb":
                return "psd";
            case ".gif":
                return "gif";
            case ".webp":
                return "webp";
            case ".svg":
                return "svg";
            case ".tif":
            case ".tiff":
                return "tiff";
            default:
                return "png";
        }
    }

    static void OpenImageInPhotopea(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("Photopea", "No se encontró el fichero:\n" + fullPath, "OK");
            return;
        }

        if (!EnsureSaveServer())
        {
            EditorUtility.DisplayDialog("Photopea", "No se pudo iniciar el servidor local de guardado.", "OK");
            return;
        }

        pendingAssetPath = assetPath.Replace('\\', '/');
        pendingFullPath = fullPath;
        pendingFormat = FormatForExtension(assetPath);

        byte[] fileBytes = File.ReadAllBytes(fullPath);
        string fileName = Path.GetFileName(fullPath);
        string htmlPath = WritePhotopeaLauncher(fileBytes, fileName, pendingAssetPath, pendingFormat, serverPort);

        if (!OpenUrl(htmlPath))
            EditorUtility.DisplayDialog("Photopea", "No se pudo abrir el navegador. Abre manualmente:\n" + htmlPath, "OK");

        PhotopeaEditorWindow window = GetWindow<PhotopeaEditorWindow>(false, null, false);
        if (window != null)
            window.RefreshStatus();
    }

    static string WritePhotopeaLauncher(byte[] fileBytes, string fileName, string assetPath, string format, int port)
    {
        string folder = Path.Combine(Path.GetTempPath(), "UnityPhotopea");
        Directory.CreateDirectory(folder);

        string htmlPath = Path.Combine(folder, "edit.html");
        string b64 = Convert.ToBase64String(fileBytes);
        string safeName = EscapeJs(fileName);
        string safeAsset = EscapeJs(assetPath);
        string saveUrl = "http://127.0.0.1:" + port + "/save";
        string saveFormat = format.Contains(":") ? format.Substring(0, format.IndexOf(':')) : format;

        // Configura File→Guardar de Photopea hacia nuestro servidor local.
        string configJson =
            "{\"server\":{\"version\":1,\"url\":\"" + saveUrl +
            "\",\"formats\":[\"" + EscapeJs(format) + "\"]}}";
        string photopeaSrc = PhotopeaUrl + "#" + Uri.EscapeDataString(configJson);

        var html = new StringBuilder(b64.Length + 4500);
        html.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<title>Photopea — ").Append(System.Net.WebUtility.HtmlEncode(fileName)).Append("</title>");
        html.Append("<style>");
        html.Append("html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:#111;font-family:Segoe UI,sans-serif}");
        html.Append("#bar{position:fixed;top:0;left:0;right:0;height:44px;display:flex;align-items:center;gap:10px;");
        html.Append("padding:0 12px;background:#1e1e1e;border-bottom:1px solid #333;z-index:10;color:#ddd;font-size:13px}");
        html.Append("#bar button{height:30px;padding:0 14px;border:0;border-radius:4px;cursor:pointer;font-weight:600}");
        html.Append("#save{background:#2ea043;color:#fff}#save:hover{background:#3fb950}");
        html.Append("#save:disabled{opacity:.5;cursor:default}");
        html.Append("#msg{flex:1;opacity:.85;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}");
        html.Append("#pp{position:fixed;top:44px;left:0;right:0;bottom:0;width:100%;height:calc(100% - 44px);border:0}");
        html.Append("</style></head><body>");
        html.Append("<div id=\"bar\">");
        html.Append("<button id=\"save\" disabled>Guardar en Unity</button>");
        html.Append("<div id=\"msg\">Cargando… Usa Archivo → Guardar (Ctrl+S) o este botón.</div>");
        html.Append("</div>");
        html.Append("<iframe id=\"pp\" src=\"").Append(photopeaSrc).Append("\" allow=\"clipboard-read; clipboard-write\"></iframe>");
        html.Append("<script>(function(){");
        html.Append("const saveUrl=\"").Append(EscapeJs(saveUrl)).Append("\";");
        html.Append("const assetPath=\"").Append(safeAsset).Append("\";");
        html.Append("const fileName=\"").Append(safeName).Append("\";");
        html.Append("const saveFormat=\"").Append(EscapeJs(saveFormat)).Append("\";");
        html.Append("const b64=\"").Append(b64).Append("\";");
        html.Append("const bin=atob(b64);const bytes=new Uint8Array(bin.length);");
        html.Append("for(let i=0;i<bin.length;i++)bytes[i]=bin.charCodeAt(i);");
        html.Append("const wnd=document.getElementById('pp').contentWindow;");
        html.Append("const saveBtn=document.getElementById('save');");
        html.Append("const msg=document.getElementById('msg');");
        html.Append("let step=0;");
        html.Append("let waitingBuffer=false;");
        html.Append("function setMsg(t){msg.textContent=t;}");
        html.Append("async function postToUnity(buf){");
        html.Append("setMsg('Enviando a Unity…');");
        html.Append("try{");
        html.Append("const r=await fetch(saveUrl,{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Unity-Asset':assetPath},body:buf});");
        html.Append("const t=await r.text();");
        html.Append("setMsg(r.ok?('Guardado: '+assetPath):(('Error Unity: '+t)||'Error al guardar'));");
        html.Append("}catch(err){setMsg('No se pudo contactar con Unity. ¿Sigue abierto el Editor?');}");
        html.Append("}");
        html.Append("function requestSave(){");
        html.Append("waitingBuffer=true;");
        html.Append("setMsg('Exportando desde Photopea…');");
        html.Append("wnd.postMessage('app.activeDocument.saveToOE(\"'+saveFormat+'\");','*');");
        html.Append("}");
        html.Append("saveBtn.addEventListener('click',requestSave);");
        html.Append("window.addEventListener('message',function(e){");
        html.Append("const d=e.data;");
        html.Append("if(d instanceof ArrayBuffer){");
        html.Append("if(waitingBuffer){waitingBuffer=false;postToUnity(d);}return;");
        html.Append("}");
        html.Append("if(typeof d!=='string')return;");
        html.Append("if(d==='Save'){requestSave();return;}");
        html.Append("if(d!=='done')return;");
        html.Append("if(step===0){step=1;wnd.postMessage(bytes.buffer,'*');return;}");
        html.Append("if(step===1){");
        html.Append("step=2;");
        html.Append("wnd.postMessage(");
        html.Append("'app.activeDocument.name=\"'+fileName+'\";'");
        html.Append("+'app.activeDocument.source=\"'+assetPath+'\";'");
        html.Append("+'app.customIO={open:\"app.echoToOE(\\\\\"Open\\\\\");\",save:\"app.echoToOE(\\\\\"Save\\\\\");\"};'");
        html.Append(",'*');");
        html.Append("saveBtn.disabled=false;");
        html.Append("setMsg('Listo · '+assetPath+' · Archivo→Guardar o botón verde');");
        html.Append("return;");
        html.Append("}");
        html.Append("});");
        html.Append("})();</script></body></html>");

        File.WriteAllText(htmlPath, html.ToString(), new UTF8Encoding(false));
        return htmlPath;
    }

    static string EscapeJs(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", "\\n");
    }

    static bool EnsureSaveServer()
    {
        if (listener != null && listener.IsListening)
            return true;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            int port = 17890 + attempt;
            try
            {
                var next = new HttpListener();
                next.Prefixes.Add("http://127.0.0.1:" + port + "/");
                next.Start();
                listener = next;
                serverPort = port;
                listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "PhotopeaSaveServer"
                };
                listenerThread.Start();
                Debug.Log("Photopea: servidor de guardado en http://127.0.0.1:" + port + "/");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Photopea: no se pudo usar el puerto " + port + ": " + ex.Message);
                try { listener?.Close(); } catch { /* ignore */ }
                listener = null;
            }
        }

        return false;
    }

    static void StopSaveServer()
    {
        try
        {
            if (listener != null)
            {
                listener.Stop();
                listener.Close();
            }
        }
        catch { /* ignore */ }

        listener = null;
        listenerThread = null;
        pendingAssetPath = null;
        pendingFullPath = null;
    }

    static void ListenLoop()
    {
        while (listener != null && listener.IsListening)
        {
            HttpListenerContext ctx = null;
            try
            {
                ctx = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Photopea listener: " + ex.Message);
                continue;
            }

            try
            {
                HandleRequest(ctx);
            }
            catch (Exception ex)
            {
                try
                {
                    WriteResponse(ctx, 500, "text/plain", "Error: " + ex.Message);
                }
                catch { /* ignore */ }
            }
        }
    }

    static void HandleRequest(HttpListenerContext ctx)
    {
        HttpListenerRequest req = ctx.Request;
        string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();

        // CORS: Photopea (origen web) llama a localhost.
        ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
        ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Unity-Asset";

        if (req.HttpMethod == "OPTIONS")
        {
            WriteResponse(ctx, 204, "text/plain", string.Empty);
            return;
        }

        if (req.HttpMethod != "POST" || path != "/save")
        {
            WriteResponse(ctx, 404, "text/plain", "Not found");
            return;
        }

        byte[] body;
        using (var ms = new MemoryStream())
        {
            req.InputStream.CopyTo(ms);
            body = ms.ToArray();
        }

        if (body == null || body.Length == 0)
        {
            WriteResponse(ctx, 400, "text/plain", "Empty body");
            return;
        }

        string assetPath = req.Headers["X-Unity-Asset"];
        byte[] fileBytes = body;

        // Formato Photopea server API: 2000 bytes JSON + binario.
        if (body.Length > JsonHeaderBytes && LooksLikePhotopeaServerPayload(body))
        {
            fileBytes = new byte[body.Length - JsonHeaderBytes];
            Buffer.BlockCopy(body, JsonHeaderBytes, fileBytes, 0, fileBytes.Length);

            try
            {
                string json = Encoding.UTF8.GetString(body, 0, JsonHeaderBytes).Trim('\0', ' ', '\r', '\n');
                int sourceIdx = json.IndexOf("\"source\"", StringComparison.Ordinal);
                if (sourceIdx >= 0)
                {
                    int q1 = json.IndexOf('"', sourceIdx + 8);
                    int q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
                    if (q1 >= 0 && q2 > q1)
                    {
                        string source = json.Substring(q1 + 1, q2 - q1 - 1);
                        if (!string.IsNullOrEmpty(source) && source.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                            assetPath = source;
                    }
                }
            }
            catch { /* usar pending path */ }
        }

        if (string.IsNullOrEmpty(assetPath))
            assetPath = pendingAssetPath;

        if (string.IsNullOrEmpty(assetPath))
        {
            WriteResponse(ctx, 400, "text/plain", "No asset path");
            return;
        }

        string fullPath = Path.GetFullPath(assetPath);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(ctx, 403, "text/plain", "Path outside project");
            return;
        }

        File.WriteAllBytes(fullPath, fileBytes);

        string responseJson = "{\"message\":\"Guardado en Unity\",\"script\":\"app.echoToOE(\\\"saved\\\");\"}";
        WriteResponse(ctx, 200, "application/json", responseJson);

        string assetPathCopy = assetPath.Replace('\\', '/');
        mainThreadQueue.Enqueue(() =>
        {
            AssetDatabase.ImportAsset(assetPathCopy, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log("Photopea: guardado " + assetPathCopy + " (" + fileBytes.Length + " bytes)");

            PhotopeaEditorWindow window = GetWindow<PhotopeaEditorWindow>(false, null, false);
            if (window != null)
            {
                window.RefreshSelectionUI();
                window.RefreshStatus();
            }
        });
    }

    static bool LooksLikePhotopeaServerPayload(byte[] body)
    {
        // El header de Photopea es JSON de 2000 bytes (relleno). Empieza por '{'
        int end = Math.Min(body.Length, 64);
        for (int i = 0; i < end; i++)
        {
            byte b = body[i];
            if (b == (byte)'{')
                return true;
            if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n' && b != 0)
                return false;
        }

        return false;
    }

    static void WriteResponse(HttpListenerContext ctx, int code, string contentType, string body)
    {
        byte[] bytes = string.IsNullOrEmpty(body) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = bytes.Length;
        if (bytes.Length > 0)
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    static void PumpMainThreadQueue()
    {
        while (mainThreadQueue.TryDequeue(out Action action))
        {
            try { action(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    static bool OpenUrl(string urlOrPath)
    {
        try
        {
            string edge = FindEdge();
            if (!string.IsNullOrEmpty(edge))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = edge,
                    Arguments = "--app=\"" + urlOrPath + "\"",
                    UseShellExecute = false
                });
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = urlOrPath,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("No se pudo abrir Photopea: " + ex.Message);
            return false;
        }
    }

    static string FindEdge()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
                return candidates[i];
        }

        return null;
    }
}
