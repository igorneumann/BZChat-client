# Bezares Chat: cliente de Windows

Programa **portable** (un único `BezaresChat.exe` de unos 200 KB, sin instalación) con el que la recepcionista y los comerciales contestan los chats de la web, WhatsApp y Facebook de [Bezares Chat](https://github.com/igorneumann/Bezares-chat).

## Uso

1. Copia `release/BezaresChat.exe` donde quieras (escritorio, carpeta de red, USB…) y ábrelo.
2. Introduce el servidor (`https://chat.bezares.com`), tu correo y tu contraseña. El administrador crea los usuarios en `https://chat.bezares.com/admin`.
   - **Mostrar contraseña**: casilla para ver lo que escribes.
   - **Recordar sesión**: si la desmarcas, no se guarda la sesión y tendrás que entrar cada vez que abras el programa.
   - **Iniciar con Windows**: el programa se abre solo al encender el ordenador, minimizado en la bandeja, y avisa igualmente de los chats. También se activa o desactiva desde el menú del icono de la bandeja.
   - **¿Has olvidado tu contraseña?**: abre la página para recibir un enlace de recuperación por correo.
3. Funciones (cada botón aparece solo si el rol del usuario tiene ese permiso; se configura en `/admin` → Configuración → Roles):
   - **Contestar chats**: escribe y pulsa *Intro* (Mayús+Intro para una línea nueva).
   - **Una persona, una conversación**: los chats con el mismo correo o teléfono aparecen en una sola fila y en un solo hilo, aunque sean de canales distintos (web, WhatsApp, Facebook) o de días distintos; un separador indica el canal y la fecha de cada chat. La barra «Respondes por…» elige por cuál se contesta y se pone en ámbar si la persona ha escrito después por otro (no cambia sola). *Resolver* y *Transferir* actúan sobre todos sus chats abiertos (tuyos o sin asignar), *Borrar* sobre todos sus chats, y *Correo* envía solo el chat por el que se responde. Los chats sin correo ni teléfono no se agrupan. En la web el correo lo escribe el visitante y no está verificado: confirma quién es antes de dar datos de sus otros chats.
   - **📎 Adjuntar archivo**: imágenes, audio, vídeo y documentos (PDF, Word, Excel, PowerPoint, OpenDocument, RTF, TXT, CSV). También se pueden **arrastrar** sobre la conversación. El tamaño máximo lo fija el administrador (16 MB por defecto).
   - **↪ Transferir**: pasa el chat a un compañero **conectado en ese momento**, con una nota opcional.
   - **📋 Copiar**: debajo de cada mensaje, copia su texto (o el enlace del adjunto) al portapapeles.
   - **Clic derecho en un mensaje**: copiar y, en los que has enviado tú, **editar** o **borrar** (borrar los de otros requiere el permiso «Borrar»). En la web, el cliente ve el cambio; en WhatsApp y Facebook solo se pueden corregir y reenviar, o borrar, los que no se entregaron.
   - **✉ Correo**: envía la transcripción completa por correo, a uno o varios destinatarios separados por comas (por defecto, el correo del cliente; cada uno recibe su propio correo). La introducción del correo se puede reescribir; admite {nombre}, {empresa}, {fecha} y {agente}. En WhatsApp, pasadas 24 h del último mensaje del cliente, al intentar contestar se pide usar el correo y lo escrito pasa a ser su texto.
   - **✓ Resuelta / ↺ Reabrir**: marca la conversación como resuelta. Se pone en verde con ✓ en la lista, deja de sonar, no se envían recordatorios y el tiempo de respuesta deja de contar desde ese momento. Si el cliente vuelve a escribir, se reabre sola.
   - **Papelera** en cada chat de la lista: borra la conversación sin abrirla, tras pedir confirmación.
   - **🗑 Borrar**: borra definitivamente la conversación, con sus mensajes y adjuntos, para todo el equipo, tras pedir confirmación. Si el visitante web vuelve a escribir, empieza un chat nuevo.
   - **🙂 Emojis**: junto a las plantillas; se insertan donde está el cursor y los últimos usados quedan en la primera pestaña. También al editar un mensaje.
   - **📝 Plantillas**: menú con respuestas preparadas que se insertan en el cuadro de texto. Desde «Gestionar plantillas…» se crean, editan y borran; se comparten entre todas las recepcionistas. Variables: `{nombre}` (cliente) y `{agente}` (tu nombre).
4. **Aviso de llamada muy visible** cuando entra un chat:
   - **timbre de teléfono** en bucle hasta que alguien lo atiende;
   - ventana roja y naranja **parpadeando, siempre encima**, con el botón **📞 ATENDER**;
   - botón de la barra de tareas e icono de la bandeja parpadeando.
   - «Posponer 2 minutos» silencia el aviso temporalmente; un chat nuevo lo vuelve a activar.
5. **⏻ Desconectarme / ▶ Conectarme** (barra inferior o menú de la bandeja): desconectado no recibes chats nuevos, avisos ni transferencias, y tus compañeros te ven desconectado. Si recepción se desconecta, los chats nuevos pasan a los comerciales conectados. Al salir del programa o cerrar sesión también apareces desconectado al momento.
6. Cerrar la ventana **no** cierra el programa: sigue en la bandeja del sistema (junto al reloj) para no perder avisos. Para salir: clic derecho en el icono → *Salir*. Desde ese menú también se silencia el sonido.

Los mensajes en otros idiomas aparecen **traducidos al español** con el original debajo, y tus respuestas se envían traducidas al idioma del cliente.

## Actualización automática

El programa comprueba cada hora (y 20 s después de abrirse) si hay una versión nueva en `{servidor}/download`. La descarga, comprueba la **firma** y la instala cuando no molesta: sin avisos sonando, sin nada a medio escribir y sin que lo estés usando. Si no está marcada «Recordar sesión», se instala al salir. También se puede pedir desde el menú del icono de la bandeja → *Buscar actualizaciones*, donde aparece la versión. Necesita estar en una carpeta con permiso de escritura (no en «Archivos de programa»).

Los administradores reciben además un aviso cuando alguien crea su cuenta con el enlace secreto, con los botones **Habilitar**, **Ignorar** y **Borrar**.

## Datos que guarda

Junto al `.exe` crea `BezaresChat.ini` con el servidor, el correo y el token de sesión **cifrado con DPAPI** (solo lo puede leer tu usuario de Windows). No guarda la contraseña. «Iniciar con Windows» añade una entrada en el registro del usuario actual (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), sin necesitar permisos de administrador. Si mueves el `.exe`, la ruta se actualiza al abrirlo. Si algo falla, escribe los detalles en `BezaresChat-errores.log`.

## Requisitos

Windows 10 u 11 (incluyen .NET Framework 4.8). No necesita instalar nada.

## Compilar

Doble clic en `build.bat`. Usa el compilador de C# que ya trae Windows y genera directamente `release\BezaresChat.exe` en unos segundos (cierra antes el programa si lo tienes abierto desde `release\`).

## Publicar una versión

`powershell -ExecutionPolicy Bypass -File publicar.ps1` pone la versión (`año.mes.día.número`), compila, firma y copia el programa y `version.json` a `..serverpublicdownload`. Después se suben los dos repositorios y se actualiza el servidor.

La clave privada está en `%USERPROFILE%.bezares-chatave-firma.xml` y no debe copiarse a git ni al servidor. **Haz una copia de seguridad**: sin ella, los programas instalados no aceptarán más actualizaciones.

## Seguridad

- Solo se conecta por **HTTPS** (admite `http://localhost` únicamente para pruebas).
- No interpreta HTML: los mensajes se muestran como texto y solo abre enlaces `http(s)` en el navegador.
- Recomendado: firmar el `.exe` con un certificado de firma de código para evitar el aviso de SmartScreen.
