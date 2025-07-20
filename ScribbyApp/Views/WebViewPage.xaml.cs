using ScribbyApp.Services;
using System.Diagnostics;
using System.Web;

// Add platform-specific using statements here
#if ANDROID
using Android.Webkit;
#elif IOS
using WebKit;
#elif WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

namespace ScribbyApp.Views
{
    public partial class WebViewPage : ContentPage
    {
        private readonly BluetoothService _bluetoothService;
        private readonly HashSet<string> _validCommands = new HashSet<string> { "w", "a", "s", "d", "x" };

        public WebViewPage(BluetoothService bluetoothService)
        {
            InitializeComponent();
            _bluetoothService = bluetoothService;

            // --- START: CODE UPDATED AS REQUESTED ---

            // 1. The HTML content from your index.html file is now in a C# string.
            var htmlContent = @"
            <!DOCTYPE html>
<head>
	<meta name=""viewport"" content=""width=device-width, user-scalable=no, minimum-scale=1.0, maximum-scale=1.0"">
	<title>Hello, AR Cube!</title>
	<!-- include three.js library -->
	<script src='js/three.js'></script>
	<!-- include jsartookit -->
	<script src=""jsartoolkit5/artoolkit.min.js""></script>
	<script src=""jsartoolkit5/artoolkit.api.js""></script>
	<!-- include threex.artoolkit -->
	<script src=""threex/threex-artoolkitsource.js""></script>
	<script src=""threex/threex-artoolkitcontext.js""></script>
	<script src=""threex/threex-arbasecontrols.js""></script>
	<script src=""threex/threex-armarkercontrols.js""></script>
</head>

<body style='margin : 0px; overflow: hidden; font-family: Monospace;'>

<!-- 
  Example created by Lee Stemkoski: https://github.com/stemkoski
  Based on the AR.js library and examples created by Jerome Etienne: https://github.com/jeromeetienne/AR.js/
-->

<script>

var scene, camera, renderer, clock, deltaTime, totalTime;

var arToolkitSource, arToolkitContext;

var patternArray, markerRootArray, markerGroupArray;
var sceneGroup;

initialize();
animate();

function initialize()
{
	scene = new THREE.Scene();

	let ambientLight = new THREE.AmbientLight( 0xcccccc, 0.5 );
	scene.add( ambientLight );
				
	camera = new THREE.Camera();
	scene.add(camera);

	renderer = new THREE.WebGLRenderer({
		antialias : true,
		alpha: true
	});
	renderer.setClearColor(new THREE.Color('lightgrey'), 0)
	renderer.setSize( 640, 480 );
	renderer.domElement.style.position = 'absolute'
	renderer.domElement.style.top = '0px'
	renderer.domElement.style.left = '0px'
	document.body.appendChild( renderer.domElement );

	clock = new THREE.Clock();
	deltaTime = 0;
	totalTime = 0;
	
	////////////////////////////////////////////////////////////
	// setup arToolkitSource
	////////////////////////////////////////////////////////////

	arToolkitSource = new THREEx.ArToolkitSource({
		sourceType : 'webcam',
	});

	function onResize()
	{
		arToolkitSource.onResize()	
		arToolkitSource.copySizeTo(renderer.domElement)	
		if ( arToolkitContext.arController !== null )
		{
			arToolkitSource.copySizeTo(arToolkitContext.arController.canvas)	
		}	
	}

	arToolkitSource.init(function onReady(){
		onResize()
	});
	
	// handle resize event
	window.addEventListener('resize', function(){
		onResize()
	});
	
	////////////////////////////////////////////////////////////
	// setup arToolkitContext
	////////////////////////////////////////////////////////////	

	// create atToolkitContext
	arToolkitContext = new THREEx.ArToolkitContext({
		cameraParametersUrl: 'data/camera_para.dat',
		detectionMode: 'mono'
	});
	
	// copy projection matrix to camera when initialization complete
	arToolkitContext.init( function onCompleted(){
		camera.projectionMatrix.copy( arToolkitContext.getProjectionMatrix() );
	});

	////////////////////////////////////////////////////////////
	// setup markerRoots
	////////////////////////////////////////////////////////////

	markerRootArray  = [];
	markerGroupArray = [];
	patternArray = [""letterA"", ""letterB"", ""letterC"", ""letterD"", ""letterF"", ""kanji""];
	
	let rotationArray = [ new THREE.Vector3(-Math.PI/2,0,0), new THREE.Vector3(0,-Math.PI/2,Math.PI/2), new THREE.Vector3(Math.PI/2, 0, Math.PI), 
		new THREE.Vector3(-Math.PI/2,Math.PI/2,0), new THREE.Vector3(Math.PI,0,0), new THREE.Vector3(0,0,0) ];
		
	for (let i = 0; i < 6; i++)
	{
		let markerRoot = new THREE.Group();
		markerRootArray.push( markerRoot );
		scene.add(markerRoot);
		let markerControls = new THREEx.ArMarkerControls(arToolkitContext, markerRoot, {
			type : 'pattern', patternUrl : ""data/"" + patternArray[i] + "".patt"",
		});
	
		let markerGroup = new THREE.Group();
		markerGroupArray.push( markerGroup );
		markerGroup.position.y = -1.25/2;
		markerGroup.rotation.setFromVector3( rotationArray[i] );
		
		markerRoot.add( markerGroup );
	}
	
	////////////////////////////////////////////////////////////
	// setup scene
	////////////////////////////////////////////////////////////
	
	sceneGroup = new THREE.Group();
	// a 1x1x1 cube model with scale factor 1.25 fills up the physical cube
	sceneGroup.scale.set(1.25/2, 1.25/2, 1.25/2);
	
	let loader = new THREE.TextureLoader();
	
	/*
	// a simple cube
	let materialArray = [
		new THREE.MeshBasicMaterial( { map: loader.load(""images/xpos.png"") } ),
		new THREE.MeshBasicMaterial( { map: loader.load(""images/xneg.png"") } ),
		new THREE.MeshBasicMaterial( { map: loader.load(""images/ypos.png"") } ),
		new THREE.MeshBasicMaterial( { map: loader.load(""images/yneg.png"") } ),
		new THREE.MeshBasicMaterial( { map: loader.load(""images/zpos.png"") } ),
		new THREE.MeshBasicMaterial( { map: loader.load(""images/zneg.png"") } ),
	];
	let mesh = new THREE.Mesh( new THREE.CubeGeometry(1,1,1), materialArray );
	sceneGroup.add( mesh );
	*/
	
	let tileTexture = loader.load(""images/tiles.jpg"");
	
	// reversed cube
	sceneGroup.add( 
		new THREE.Mesh(
			new THREE.BoxGeometry(2,2,2),
			new THREE.MeshBasicMaterial({
				map: tileTexture,
				side: THREE.BackSide,
			})
		)
	);
	
	// cube vertices
	
	let sphereGeometry = new THREE.SphereGeometry(0.20, 6,6);
	
	let sphereCenters = [ 
		new THREE.Vector3(-1,-1,-1), new THREE.Vector3(-1,-1,1), new THREE.Vector3(-1,1,-1), new THREE.Vector3(-1,1,1),
		new THREE.Vector3( 1,-1,-1), new THREE.Vector3( 1,-1,1), new THREE.Vector3( 1,1,-1), new THREE.Vector3( 1,1,1),
	];
	
	let sphereColors = [ 0x444444, 0x0000ff, 0x00ff00, 0x00ffff, 0xff0000, 0xff00ff, 0xffff00, 0xffffff ];
	
	for (let i = 0; i < 8; i++)
	{
		let sphereMesh = new THREE.Mesh( 
			sphereGeometry, 
			new THREE.MeshLambertMaterial({
				map: tileTexture,
				color: sphereColors[i]
			})
		);
		sphereMesh.position.copy( sphereCenters[i] );
		sceneGroup.add(sphereMesh);
	}
	
	// cube edges
	
	let edgeGeometry = new THREE.CylinderGeometry( 0.05, 0.05, 2, 32 );
	
	let edgeCenters = [
		new THREE.Vector3(0, -1, -1), new THREE.Vector3(0, 1, -1), new THREE.Vector3(0, -1, 1), new THREE.Vector3(0, 1, 1),
		new THREE.Vector3(-1, 0, -1), new THREE.Vector3(1, 0, -1), new THREE.Vector3(-1, 0, 1), new THREE.Vector3(1, 0, 1),
		new THREE.Vector3(-1, -1, 0), new THREE.Vector3(1, -1, 0), new THREE.Vector3(-1, 1, 0), new THREE.Vector3(1, 1, 0)
	];
	
	let edgeRotations = [
		new THREE.Vector3(0,0,Math.PI/2), new THREE.Vector3(0,0,Math.PI/2), new THREE.Vector3(0,0,Math.PI/2), new THREE.Vector3(0,0,Math.PI/2),
		new THREE.Vector3(0,0,0), new THREE.Vector3(0,0,0), new THREE.Vector3(0,0,0), new THREE.Vector3(0,0,0),
		new THREE.Vector3(Math.PI/2,0,0), new THREE.Vector3(Math.PI/2,0,0), new THREE.Vector3(Math.PI/2,0,0), new THREE.Vector3(Math.PI/2,0,0)
	];
	
	let edgeColors = [
		0x880000, 0x880000, 0x880000, 0x880000,
		0x008800, 0x008800, 0x008800, 0x008800,
		0x000088, 0x000088, 0x000088, 0x000088
	];
	
	for (let i = 0; i < 12; i++)
	{
		let edge = new THREE.Mesh(
			edgeGeometry,
			new THREE.MeshLambertMaterial({ 
				map: tileTexture,
				color: edgeColors[i] 
			})
		);
		edge.position.copy( edgeCenters[i] );
		edge.rotation.setFromVector3( edgeRotations[i] );

		sceneGroup.add(edge);
	}
	
	sceneGroup.add(
		new THREE.Mesh(
			new THREE.TorusKnotGeometry(0.5, 0.1),
			new THREE.MeshNormalMaterial()
		)
	);
	
	let pointLight = new THREE.PointLight( 0xffffff, 1, 50 );
	pointLight.position.set(0.5,3,2);
	scene.add( pointLight );
}


function update()
{
	// update artoolkit on every frame
	if ( arToolkitSource.ready !== false )
		arToolkitContext.update( arToolkitSource.domElement );
	
	for (let i = 0; i < 6; i++)
	{
		if ( markerRootArray[i].visible )
		{
			markerGroupArray[i].add( sceneGroup );
			console.log(""visible: "" + patternArray[i]);
			break;
		}
	}
	
}


function render()
{
	renderer.render( scene, camera );
}


function animate()
{
	requestAnimationFrame(animate);
	deltaTime = clock.getDelta();
	totalTime += deltaTime;
	update();
	render();
}

</script>

</body>
</html>";

            // 2. A new HtmlWebViewSource is created.
            var htmlSource = new HtmlWebViewSource { Html = htmlContent };

            // IMPORTANT: See the note below about BaseUrl.
            // Without this, relative links to CSS, JS, and other pages will NOT work.
            // htmlSource.BaseUrl = "???"; // This needs to be set correctly.

            // 3. The WebView's source is set to your hardcoded HTML content.
            MyWebView.Source = htmlSource;

            // --- END: CODE UPDATE ---

            this.Loaded += WebViewPage_Loaded;
        }

        // ... THE REST OF YOUR WebViewPage.xaml.cs FILE REMAINS THE SAME ...
        // Make the Loaded event handler async so we can use await inside
        private async void WebViewPage_Loaded(object? sender, EventArgs e)
        {
            await RequestWebRtcPermissions();
            ConfigureNativeWebView();
        }

        protected override bool OnBackButtonPressed()
        {
            if (MyWebView.CanGoBack)
            {
                MyWebView.GoBack();
                return true;
            }
            return base.OnBackButtonPressed();
        }

        private async Task RequestWebRtcPermissions()
        {
            var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            var microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();

            if (cameraStatus != PermissionStatus.Granted || microphoneStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permissions Required", "Camera and Microphone permissions are needed for WebRTC features. Please enable them in app settings if you change your mind.", "OK");
            }
        }

        private async void ConfigureNativeWebView()
        {
            if (MyWebView.Handler?.PlatformView == null)
            {
                return;
            }

#if ANDROID
            var platformView = MyWebView.Handler.PlatformView as global::Android.Webkit.WebView;
            if (platformView != null)
            {
                platformView.Settings.JavaScriptEnabled = true;
                platformView.Settings.MediaPlaybackRequiresUserGesture = false;
                platformView.Settings.AllowFileAccess = true;
                platformView.Settings.AllowFileAccessFromFileURLs = true;
                platformView.Settings.AllowUniversalAccessFromFileURLs = true;
                platformView.SetWebChromeClient(new CustomWebChromeClient());
            }
#elif IOS
            var platformView = MyWebView.Handler.PlatformView as WKWebView;
            if (platformView != null)
            {
                platformView.Configuration.AllowsInlineMediaPlayback = true;
                platformView.Configuration.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
            }
#elif WINDOWS
            var platformView = MyWebView.Handler.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
            if (platformView != null)
            {
                try
                {
                    await platformView.EnsureCoreWebView2Async();
                    platformView.CoreWebView2.PermissionRequested += (sender, args) =>
                    {
                        if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                            args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                        {
                            args.State = CoreWebView2PermissionState.Allow;
                        }
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error configuring WebView2: {ex.Message}");
                }
            }
#endif
        }

        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url == null) return;

            if (e.Url.StartsWith("scribby://"))
            {
                e.Cancel = true;
                if (!_bluetoothService.IsConnected || _bluetoothService.PrimaryWriteCharacteristic == null)
                {
                    StatusLabel.Text = "Error: Cannot send command, not connected.";
                    return;
                }
                try
                {
                    var uri = new Uri(e.Url);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string? command = query["command"];

                    if (!string.IsNullOrEmpty(command) && _validCommands.Contains(command))
                    {
                        await SendCommandInternalAsync(command);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing WebView command: {ex.Message}");
                }
            }
        }

        protected override void OnAppearing() { base.OnAppearing(); UpdateStatus(); }
        private void UpdateStatus() { StatusLabel.Text = _bluetoothService.IsConnected ? $"Status: Connected to {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}." : "Status: Not connected."; }
        private async Task SendCommandInternalAsync(string command) { if (_bluetoothService.PrimaryWriteCharacteristic != null) await _bluetoothService.SendCommandAsync(_bluetoothService.PrimaryWriteCharacteristic, command); }
    }

#if ANDROID
    internal class CustomWebChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest request)
        {
            request.Grant(request.GetResources());
        }
    }
#endif
}