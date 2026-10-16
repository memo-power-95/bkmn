'MOD@@ Guillermo Carrillo - Install PCB LINEA 1: prueba de toma (v1.0.6.7.6) + velocidad (v2.1.0)
'  Base: Main de la linea 1 (sus puntos, su NG y su colocacion: SIN CAMBIOS).
'  Prueba de toma: 019 / 020 / 021 + funciones al final (usa P47).
'  Velocidad:
'  1. Toma (010), colocacion (011) y posicion (015) ya no leen ni reescriben robot1.pts.
'  2. Colocacion: sin el Move que no movia; la espera bajo de 0.1 a 0.05 s.
'  3. La foto (003) contesta tambien la posicion del robot (la PC ya no manda el 015).
'  4. Bajar el cilindro mientras el brazo llega (AntToma / AntColoca). APAGADO de fabrica.
'  5. I21 = 0 al recibir 004, 013 y 014.
'  v2.2 (comparacion con la Top Assy). Todo se fija en main; los valores de fabrica dejan la maquina IGUAL:
'  6. AccelFoto: aceleracion para ir a la foto (003) y al escaneo (004). Antes fijo 70.
'  7. AccelTrabajo: aceleracion para tomar (010) y colocar (011). Antes se quedaba en 70 desde la foto.
'  8. TomaDirecta: tomar como la Top Assy (Go directo al punto y bajar el cilindro). APAGADO.
'  9. Colocacion: al terminar bien regresa AccelS 15000, 8000 (dejaba 1000, 1000 por descuido).
'  La colocacion con el sensor de presion (bajada lenta, Till, E1642-E1644): SIN CAMBIOS.

'--- global variable definition  ---
       'S3$(0)        function code   
       'S3$(1)        position indel  
       'S3$(2)        gripper indel   
       'S3$(3)        vision_X        
       'S3$(4)        vision_Y        
       'S3$(5)        vision_U         
       'S3$(6)        character terminator  
       'S3$(7-12)     save "_"        

Global Preserve Boolean B1, B2, B3, B4, B5, B6, B7, B8, B9, B10, B11, B12, B13, B14, B15, B16, B17, B18, B19, B20, B21, B22, B23, B24, B25
Global Preserve Integer I1, I2, I3, I4, I5, I6, I7, I8, I9, I10, I11, I12, I13, I14, I15, I16, I17, I18, I19, I20, I21
Global Preserve Real R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R14, R15, R16, R17, R18, R19, R20(20), R21
Global Preserve String S1$, S2$, S3$(15), S4$, S5$, S6$, S7$, S8$, S9$, S10$, S11$, S12$, S13$, S14$, S15$, S16$, S17$, S18$, S19$, S20$(30)
Global Integer CalTomaGripper   'MOD@@ v1.0.6.7.6 prueba de toma (gripper abajo, 0 = ninguno)
Global Boolean AntToma, AntColoca   'MOD@@ v2.1.0 opcion 4 (se fijan en main)
Global Integer AccelFoto, AccelTrabajo   'MOD@@ v2.2 opciones 6 y 7 (se fijan en main)
Global Boolean TomaDirecta   'MOD@@ v2.2 opcion 8 (se fija en main)

Function main
''''''''''''''''''''''''''''''' TOP_TLA_Station_3  '''''''''''''''''''''''''''''''''''''

'--- Load the robot point file  ---  
LoadPoints "robot1.pts"
  
'  Xqt MonitorIO;
'  Xqt PCnet;
'  
'--- initialization  ---
Call Init
  
'--- Robot parameter settings  ---
Tool 0
Weight 5                      'load setting 
Motor On                      'Power up the motor 
'Fine 3, 3, 3, 3              '4 axis positioning accuracy setting 
FineDist 0.1                  'Target point positioning accuracy setting（mm）
Power High                    'High power mode setting 
Speed 100                     'Internal speed setting  （％）  
SpeedS 2000                   'Linear speed setting    （mm/sec）
SpeedFactor I1                'External speed setting (%) 
Accel 100, 100                'Acceleration and deceleration ratio setting (%)  
AccelS 15000, 8000            'Acceleration and deceleration value setting (mm/sec2)  
PTPBoost 75, 75, 75           'Nudge parameter setting   
'LimitTorque 20, 20, 20, 20   'Torque limit setting 

'--- Detect the status and start of background task No. 65  ----
'MOD@@ Guillermo Carrillo - v2.1.0 opcion 4: bajar el cilindro del gripper mientras el brazo llega
'  APAGADO de fabrica. Para probarlo: cambiar a True, Build y cargar.
AntToma = False
AntColoca = False

'MOD@@ Guillermo Carrillo - v2.2 opciones 6, 7 y 8. Con estos valores la maquina queda IGUAL que antes.
'  Para probar, cambiar UNO a la vez, Build e Inicializar:
'  AccelFoto: 70 = como antes. Probar 85 y luego 100. Si la vision empieza a fallar (foto movida), regresar.
'  AccelTrabajo: 70 = como antes (la foto la dejaba en 70). Probar 100. Si la toma sale descentrada, 85.
'  TomaDirecta: False = como antes. True = como la Top Assy (sin acercamiento de 5 mm ni bajada lenta).
AccelFoto = 70
AccelTrabajo = 70
TomaDirecta = False
If AccelFoto < 10 Or AccelFoto > 100 Then AccelFoto = 70        'valor fuera de rango: como antes
If AccelTrabajo < 10 Or AccelTrabajo > 100 Then AccelTrabajo = 70

If TaskState(65) = 0 Then
	Xqt 65, bgmain
EndIf
'  
'--- Normal workflow (running in a loop)   ---
Do
  SpeedFactor I1     'External speed setting (%) 
  Select S2$
    Case "001"
      Call PhotoFeedTray(2)    'Taking pictures of the feeding tray 
    Case "002"
  	  Call ScanFeedTray(R1)     'Feed tray scan code 
  	Case "003"
  	  Call PhotoFeedDUT(42, R1)     'Taking pictures of incoming products  
  	Case "004"
  	  I21 = 0   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 5: la herramienta de la foto (I21) ya no se usa
  	  Call ScanFeedDUT(6, R1)      'Incoming product scan code
    Case "005"
      Call PhotoDischargeTray(8)   'Taking pictures of the discharge tray
    Case "006"
      Call ScanDischargeTray(36, R1)    'Outlet tray scan code
    Case "007"
      Call PhotoDischargeDUT(11, R1)   'Outlet tray product photo
    Case "008"
      Call ScanDischargeDUT(12, R1)    'Outlet tray product scan code
    Case "009"
      Call ScanDUTBelow(13, R2)        'Scan the code under the product
    Case "010"
       If B17 = True Then
          Call TakeFeedDUT_1(13, R1, R2, R3, R4, R5) 'Feed product recharging 	
       Else
          Call TakeFeedDUT(13, R1, R2, R3, R4, R5) 'without visiton model
       EndIf
  	Case "011"
  	   If B17 = True Then
		  Call PutDischargeDUT_2(121, R1, R2)
       Else
          Call PutDischargeDUT(121, R1, R2, R3, R4, R5) 'without visual mode
       EndIf
    Case "013"
      I21 = 0   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 5: la herramienta de la foto (I21) ya no se usa
      Call BackSafe                                  'back to safe position
    Case "014"
      I21 = 0   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 5: la herramienta de la foto (I21) ya no se usa
      Call VisionCalibration(R1, R3, R4, R5)          'visual calibration
    Case "015"                                    'Get the current position of the robot 
      Call GetCurrentPosition_1
    Case "016"
      Call Throw_NG(R1, R2)                    'NG position throwing 
    Case "017"
      Call Photo_NG(R1) 'NG position throwingNG
    Case "019"                                    'MOD@@ v1.0.6.7.6 prueba de toma: bajar sin tomar
      Call CalTomaIr(13, R1, R2, R3, R4, R5)
    Case "020"                                    'MOD@@ v1.0.6.7.6 prueba de toma: corrimiento
      Call CalTomaJog(R3, R4, R5)
    Case "021"                                    'MOD@@ v1.0.6.7.6 prueba de toma: subir gripper
      Call CalTomaSalir
      
'    Case "012"  'Get robot position 
'    	Call GetCurrentPosition(R1)
'    	S2$ = ""
'    Case "105"  'Read Robot Speed  
'    	Call ReadRobotSpeed
'    	S2$ = ""
'    Case "018"  'visual switch
'    	Call photo_change(R1)
'    	S2$ = ""

      
    Default
      Wait 0.01
  Send
Loop
    
Fend
Function Throw_NG(bli31 As Integer, bli32 As Integer)
 
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place

'--- Check the gripper index  ---
  If bli32 < 1 Or bli32 > 3 Then
	    S11$ = S2$ + "_E1620_a"; B1 = True
	    Print "Gripper index exception"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf

  '--- Check the gripper index  ---
  If bli31 < 1 Or bli31 > 18 Then
	    S11$ = S2$ + "_E1620_a"; B1 = True
	    Print "Position index exception"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
  
    Integer bli55
   If bli32 = 1 Then
     bli55 = 200
   ElseIf bli32 = 2 Then
     bli55 = 218
   ElseIf bli32 = 3 Then
     bli55 = 236
   EndIf
   '        236  +     5   -1=240
   bli55 = bli55 + bli31 - 1
  
  '--- Get the current robot position  ---
  LoadPoints "robot1.pts"
  P99 = CurPos
  SavePoints "robot1.pts"
  
  '--- Robot motion transition ---
  If CZ(P99) < CZ(P(bli55)) - 2 Then
     CZ(P99) = CZ(P(bli55))
     Move P99
  EndIf
  If CY(P99) <= 130 Then
     Go P0 CP
  EndIf
  
Hand P(bli55), Lefty
'---Move to +20mm above NG ---  
Go P(bli55) +Z(15) /L

SpeedS 1000; AccelS 2000, 2000
Call GripperDown(R2)
Go P(bli55) /L

'open gripper 
Call GripperClamp(R2)
Wait 0.1
  
'---Move to +20mm above NG ---    
Move P(bli55) +Z(15) /L
Call GripperUp(R2)

Wait 0.01
S2$ = ""
S11$ = "016_OKOK_a"; B1 = True
Print "Robot NG throwing completed"
SpeedS 2000; AccelS 15000, 8000
		
Fend

Function Photo_NG(bli41 As Integer)

Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place
Tool 0

'--- Check the NG photo location index ---
If bli41 < 1 Or bli41 > 18 Then
    S11$ = S2$ + "_E1631_a"; B1 = True
    Print "NG bit photo index is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
EndIf

'--- Get the current robot position  ---
LoadPoints "robot1.pts"
P99 = CurPos
SavePoints "robot1.pts"

'--- Robot motion transition ---
If CZ(P99) < CZ(P(67)) - 2 Then
 CZ(P99) = CZ(P(67))
 Move P99
EndIf
If CY(P99) < 180 Then
 Go P0 CP
EndIf

'---NG location photo spot ---
R10 = -45; R11 = 30; I3 = bli41; I8 = 6 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction) 
Call PalletOffset
Hand P(67), Lefty
Go P(67) +X(R12) +Y(R13) /L

Wait 0.1
S2$ = ""
S11$ = "017_OKOK_a"; B1 = True
Print "Robot arrives at NG photo spot"
	
Fend
Function TEST_GO
	Tool 0
	Weight 5                      'load setting 
	Motor On                      'Power up the motor 
	'Fine 3, 3, 3, 3              '4 axis positioning accuracy setting 
	FineDist 0.1                  'Target point positioning accuracy setting
	Power High                    'High power mode setting 
	Speed 100                     'Internal speed setting  （％）  
	SpeedS 2000                   'Linear speed setting    （mm/sec）
	SpeedFactor I1                'External speed setting (%) （％）
	Accel 100, 100                'Acceleration and deceleration ratio setting (%)  
	AccelS 15000, 8000            'Acceleration and deceleration value setting (mm/sec2)  
	PTPBoost 75, 75, 75           'Nudge parameter setting   
	'LimitTorque 20, 20, 20, 20   'Torque limit setting 
	
Go P5

Fend


Function WriteRobotSpeed
'''''''''''''''''''''''''''''''' Write robot external speed '''''''''''''''''''''''''''''''''''''   

'--- Check robot external speed data  ---
If R21 < 1 Then
  R21 = 1
ElseIf R21 > 100 Then
  R21 = 100
EndIf

I1 = R21 'Robot external speed write
S11$ = "104_OKOK_a"; B1 = True
Print "Write robot external speed complete"
    
Fend
Function ReadRobotSpeed
'''''''''''''''''''''''''''''''' Read robot external speed '''''''''''''''''''''''''''''''''''''   

'--- Robot external speed character length reading ---
S6$ = Str$(I1)
I7 = Len(S6$)

'--- Completion of the character length of the robot's external speed ---
If I7 = 1 Then
  S6$ = "00" + S6$
ElseIf I7 = 2 Then
  S6$ = "0" + S6$
ElseIf I7 = 3 Then
  S6$ = S6$
EndIf

S11$ = S2$ + "_" + S6$ + "_OKOK_a"; B1 = True
Print "Reading robot external speed completed"
    
Fend
Function Init
''''''''''''''''''''''''''''''' initialization '''''''''''''''''''''''''''''''''''''   

'--- initialize boolean variable ---  
B1 = False; B2 = False; B3 = False; B4 = False; B17 = True; B19 = False; B20 = True; B21 = True; B22 = True
Print "Init again！！！！！！！！！"

'--- Initialize integer variables ---  
I1 = 30; I2 = 0; I3 = 0; I4 = 0; I5 = 10; I6 = 0; I7 = 0; I20 = 0; I21 = 0

'--- initialize real variables ---  
R1 = 0; R2 = 0; R3 = 0; R4 = 0; R5 = 0; R10 = 0; R11 = 0; R11 = 0; R12 = 0; R13 = 0; R15 = 0; R16 = 0; R17 = 0; R18 = 0

'--- initialize string variable --- 
S3$(0) = "";  S2$ = ""; S4$ = ""; S5$ = ""; S12$ = ""; S13$ = ""; S15$ = ""; S16$ = ""; S17$ = ""; S18$ = "";
   
'--- Initialize Robot IO ---    
Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 
Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 	

      
Fend
Function PhotoFeedTray(bli01 As Integer)
''''''''''''''''''''''''''''''' Taking pictures of the feeding tray  '''''''''''''''''''''''''''''''''''''   

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place
Tool 0
  '--- Get the current robot position  ---
  LoadPoints "robot1.pts"
  P99 = CurPos
  SavePoints "robot1.pts"
  
  '--- Robot motion transition ---
  If CZ(P99) < CZ(P(bli01)) - 2 Then
     CZ(P99) = CZ(P(bli01))
     Move P99
  EndIf
  If CY(P99) > 130 Then
     Go P0 CP
  EndIf
  
  Go P(bli01) 'Feeding tray photo position 
S2$ = ""
S11$ = "001_OKOK_a"; B1 = True
Print "The robot arrives at the feed tray photo point"
   
Fend
Function ScanFeedTray(bli02 As Integer)
''''''''''''''''''''''''''''''' Feed tray scan code '''''''''''''''''''''''''''''''''''''   

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place
  Tool 0
  '--- Get the current robot position  ---
'  LoadPoints "robot1.pts"
'  P99 = CurPos
'  SavePoints "robot1.pts"
  
  '--- Robot motion transition ---
'  If CZ(P99) < CZ(P(bli02 + 31)) - 2 Then
'     CZ(P99) = CZ(P(bli02 + 31))
'     Move P99
'  EndIf
'  If CY(P99) > 130 Then
'     Go P0 CP
'  EndIf
  
  Go P(bli02 + 31) 'Feeding tray scan position 

'Wait 0.1
S2$ = ""
S11$ = "002_OKOK_a"; B1 = True
Print "The robot reaches the scanning point of the feed tray"
   
Fend
Function PhotoFeedDUT(bli03 As Integer, bli04 As Integer)
''''''''''''''''''''''''''''''' Taking pictures of incoming products '''''''''''''''''''''''''''''''''''''   

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On
  Tool 0

'--- Check the photo location index of incoming products  ---
     If bli04 < 1 Or bli04 > 42 Then
	    S11$ = S2$ + "_E1623_a"; B1 = True
	    Print "Incoming product photo index is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
	 EndIf
Integer to_1, to_2
If bli04 > 21 Then
 B2 = True
EndIf

If B2 = True Then
R10 = 126
Else
bli03 = bli03 + 6
R10 = -126
EndIf
If B2 = True And bli04 = 42 Then
  B2 = False
EndIf


If bli04 > 21 Then
	bli04 = bli04 - 21
EndIf

  to_2 = 100
  to_1 = bli04 /3
  to_2 = bli04 Mod 3
  If to_2 = 0 Then
  	to_2 = 3
  EndIf
  If to_2 < 1 Or to_2 > 3 Then
  		S11$ = S2$ + "_E1623_a"; B1 = True
	    Print "Incoming product photo index is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
  bli03 = bli03 + to_2 - 1
  I21 = to_2
  

'---Incoming product photo spot---
Print "tool="; Print to_2
Tool to_2

 R11 = -32; I3 = bli04; I8 = 3 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction)
Call PalletOffset
Hand P(bli03), Lefty
Accel AccelFoto, AccelFoto   'MOD@@ Guillermo Carrillo - v2.2 opcion 6 (antes: Accel 70, 70)
Go P(bli03) +X(R12) +Y(R13) /L

S2$ = ""
'MOD@@ Guillermo Carrillo - v2.1.0 opcion 3: contesta tambien la posicion, igual que el 015.
'  Una PC con version anterior lo sigue leyendo como OK.
P99 = CurPos
S11$ = "003_" + Str$(CX(P99)) + "_" + Str$(CY(P99)) + "_" + Str$(CU(P99)) + "_OKOK_a"; B1 = True
Print "The robot arrives at the incoming product photo spot"
   
Fend
Function ScanFeedDUT(bli05 As Integer, bli06 As Integer)
''''''''''''''''''''''''''''''' Incoming product scan code '''''''''''''''''''''''''''''''''''''   

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On
Tool 0
'--- Check the scanning code position index of incoming products ---
  If bli06 < 1 Or bli06 > 42 Then
	    S11$ = S2$ + "_E1624_a"; B1 = True
	    Print "Incoming product scan code index is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
Tool 0

If bli06 > 21 Then
 B3 = True
EndIf

If B3 = True Then
R10 = 126
Else
bli05 = bli05 + 1
R10 = -126
EndIf

If B3 = True And bli06 = 42 Then
  B3 = False
EndIf



If bli06 > 21 Then
	bli06 = bli06 - 21
EndIf



'---Incoming product scan code point---
 R11 = -32; I3 = bli06; I8 = 3 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction)
Call PalletOffset
Hand P(bli05), Lefty
Accel AccelFoto, AccelFoto   'MOD@@ Guillermo Carrillo - v2.2 opcion 6 (antes: Accel 70, 70)
Go P(bli05) +X(R12) +Y(R13) /L

'Wait 0.1
S2$ = ""
S11$ = "004_OKOK_a"; B1 = True
Print "The robot arrives at the scanning point of the incoming product"
   
Fend
Function PhotoDischargeTray(bli07 As Integer)
''''''''''''''''''''''''''''''' Taking pictures of the discharge tray  '''''''''''''''''''''''''''''''''''''   
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 
Tool 0
  '--- Get the current robot position  ---
  LoadPoints "robot1.pts"
  P99 = CurPos
  SavePoints "robot1.pts"
  
  '--- Robot motion transition ---
  If CZ(P99) < CZ(P(bli07)) - 2 Then
     CZ(P99) = CZ(P(bli07))
     Move P99
  EndIf
  If CY(P99) <= 130 Then
     Go P0 CP
  EndIf
  
  Go P(bli07) /L  'Discharge tray photo spot 

  S2$ = ""
  S11$ = "005_OKOK_a"; B1 = True
  Print "The robot reaches the photo point of the out tray"
   
Fend
Function ScanDischargeTray(bli08 As Integer, bli21 As Integer)
''''''''''''''''''''''''''''''' Outlet tray scan code  '''''''''''''''''''''''''''''''''''''    

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 
Tool 0
'--- Check the index of the scanning code position of the discharge tray  ---
  If bli21 < 1 Or bli21 > 4 Then
  	    Wait 2
	    S11$ = S2$ + "_E1625_a"; B1 = True
	    Print "The index of the scanning code position of the discharge tray is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
   EndIf

  bli08 = bli08 + bli21

  
  Go P(bli08) /L

	S2$ = ""
	S11$ = "006_OKOK_a"; B1 = True
	Print "The robot reaches the scan code point of the discharge tray"
   
Fend
Function PhotoDischargeDUT(bli09 As Integer, bli10 As Integer)
''''''''''''''''''''''''''''''' Taking pictures of outgoing products  '''''''''''''''''''''''''''''''''''''    

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 
Tool 0
'--- Check the index of the photo location of the outgoing product  ---
  If bli10 < 1 Or bli10 > 6 Then
	    S11$ = S2$ + "_E1626_a"; B1 = True
	    Print "The index of the photo position of the outgoing product is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
  
  
R10 = -120; R11 = 50; I3 = bli10; I8 = 3 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction) 
Call PalletOffset

Hand P(bli09), Lefty
Go P(bli09) +X(R12) +Y(R13) /L  'Outlet product photo spot 

	S2$ = ""
	S11$ = "007_OKOK_a"; B1 = True
	Print "The robot arrives at the shooting point of the outgoing product"
   
Fend
Function ScanDischargeDUT(bli11 As Integer, bli12 As Integer)
''''''''''''''''''''''''''''''' Outgoing product scan code  '''''''''''''''''''''''''''''''''''''    
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 
   
'--- Check the index of the scanning code position of the outgoing product  ---
Lable_10:
  If bli12 < 1 Or bli12 > 12 Then
	    S11$ = S2$ + "_E1626_a"; B1 = True
	    Print "The index of the scanning code position of the output product is abnormal"
        Pause
	    Wait 2
	    GoTo Lable_10
  EndIf
   
  '--- Get the current robot position  ---
  LoadPoints "robot1.pts"
  P99 = CurPos
  SavePoints "robot1.pts"
  
  '--- Robot motion transition ---
'  If CZ(P99) < CZ(P(bli11)) - 2 Then
'     CZ(P99) = CZ(P(bli11))
'     Move P99
'  EndIf
  If CY(P99) <= 130 Then
     Go P0 CP
  EndIf
    
R10 = 60; R11 = -50; I3 = bli12; I8 = 6 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction) 
Call PalletOffset
Hand P(bli11), Lefty
Go P(bli11) +X(R12) +Y(R13) /L  'Outgoing product scan code point 
 
'	Wait 0.1
	S2$ = ""
	S11$ = "008_OKOK_a"; B1 = True
	Print "The robot arrives at the scanning point of the outgoing product"
   
Fend
Function ScanDUTBelow(bli13 As Integer, bli14 As Integer)
''''''''''''''''''''''''''''''' Scan the code below the product  '''''''''''''''''''''''''''''''''''''    
   
''--- Check the scan code gripper index below the product  ---
'Lable_11:
'  If bli14 < 1 Or bli14 > 3 Then
'  	 Wait 2
'	    S11$ = S2$ + "_E1626_a"; B1 = True
' Print "The index of the scanner under the product is abnormal"
'       pause
'	    GoTo Lable_11
'  EndIf
'   
'bli13 = bli13 + bli14 'Calculate the scan code position index under the product 
'Jump P(bli13) /L LimZ CZ(P0) 'Scan the code below the product 
Wait 0.1
S2$ = ""
S11$ = "009_OKOK_a"; B1 = True
Print "The robot reaches the scanning point under the product"
   
Fend
Function TakeFeedDUT(bli15 As Integer, bli16 As Integer, bli17 As Integer, blr01 As Real, blr02 As Real, blr03 As Real)
''''''''''''''''''''''''''''''' Incoming product reclaiming  '''''''''''''''''''''''''''''''''''''    
   
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 
   
'--- Check Incoming Product Reclaim Location Index  ---
Lable_12:
  If bli16 < 1 Or bli16 > 42 Then
	    S11$ = S2$ + "_E1627_a"; B1 = True
	    Print "The index of the reclaim position of the incoming product is abnormal"
        Pause
	    Wait 2
	    GoTo Lable_12
  EndIf
   
'--- Check the index of the pick-up gripper for the incoming product  ---
Lable_13:
  If bli17 < 1 Or bli17 > 3 Then
	    S11$ = S2$ + "_E1628_a"; B1 = True
	    Print "The index of the pick-up gripper for the incoming product is abnormal"
        Pause
	    Wait 2
	    GoTo Lable_13
  EndIf
  
'--- Check incoming product reclaim visual data  ---
blr01 = 0 'LH-0802
blr02 = 0
blr03 = 0

   Lable_18:
   	 If (blr01 < -5 Or blr01 > 5) Or (blr02 < -5 Or blr02 > 5) Or (blr03 < -5 Or blr03 > 5) Then
	      S11$ = S2$ + "_E1628_a"; B1 = True
	      Print "Incoming Product Reclaiming Visual Data Abnormal"
          Pause
	       Wait 2
	      GoTo Lable_18
	 EndIf
   Print "Visual data X="; Print blr01
   Print "Visual data Y="; Print blr02
   Print "Visual data U="; Print blr03




'---- Tool coordinate switch  ----
  Tool bli17
If bli16 > 21 Then
 B4 = True
EndIf

If B4 = True Then
R10 = 126
Else
bli15 = bli15 + 4
R10 = -126
EndIf

If B4 = True And bli16 = 42 Then
  B4 = False
EndIf



If bli16 > 21 Then
	bli16 = bli16 - 21
EndIf

 Integer to_1, to_2
  to_2 = 100
  to_1 = bli16 /3
  to_2 = bli16 Mod 3
  If to_2 = 0 Then
  	to_2 = 3
  EndIf
  If to_2 < 1 Or to_2 > 3 Then
  		S11$ = S2$ + "_E1628_a"; B1 = True
	    Print "Incoming product photo index is abnormal"
	    Pause
	    Wait 2
	    GoTo Lable_12
  EndIf
  bli15 = bli15 + to_2
  I21 = to_2
 
 R11 = -32;  I3 = bli16; I8 = 3 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction) 
	Call PalletOffset
	

	
'R12 = R12 + blr01; R13 = R13 + blr02 'Calculate X/Y Offset 
Hand P(bli15), Lefty

'---+20mm above the take-up point of the incoming product ---
Call GripperClamp(R2)
If B21 = False Then
 Go P(bli15) +X(R12) +Y(R13) +Z(20) +U(blr03) /L
Else
 Go P(bli15) +X(R12) +Y(R13) +Z(0) +U(blr03) /L
EndIf
'Go P(bli15) +X(R12) +Y(R13) +Z(0) +U(blr03) /L

Call GripperDown(R2)

 '---Incoming product take-up point ---
SpeedS 1000; AccelS 1000, 1000
If B21 = False Then
 Move P(bli15) +X(R12) +Y(R13) +Z(20) +U(blr03) /L
Else
 Move P(bli15) +X(R12) +Y(R13) +U(blr03) /L
EndIf
'Move P(bli15) +X(R12) +Y(R13) +U(blr03) /L
'Call GripperOpen(R2)
If R2 = 1 Then
Off G1Clamp
On G1Open
ElseIf R2 = 2 Then
Off G2Clamp
On G2Open
ElseIf R2 = 3 Then
Off G3Clamp
On G3Open
EndIf


'--- +20mm above the take-up point of the incoming product ---
Wait 0.2
SpeedS 2000; AccelS 15000, 8000
If B21 = False Then
 Move P(bli15) +X(R12) +Y(R13) +Z(20) +U(blr03) /L
Else
 Move P(bli15) +X(R12) +Y(R13) +Z(0) +U(blr03) /L
EndIf
'Move P(bli15) +X(R12) +Y(R13) +Z(0) +U(blr03) /L
Call GripperUp(R2)

S2$ = ""
S11$ = "010_OKOK_a"; B1 = True
Print "The robot reaches the pick-up point for the incoming product"
SpeedS 2000; AccelS 15000, 8000
Tool 0
   
Fend
Function TakeFeedDUT_1(bli15 As Integer, bli16 As Integer, bli17 As Integer, blr01 As Real, blr02 As Real, blr03 As Real)
	
''''''''''''''''''''''''''''''' Feed product recharging '''''''''''''''''''''''''''''''''''''     
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Grip 1/2/3 up
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for gripper 1/2/3 to rise into position
  Accel AccelTrabajo, AccelTrabajo   'MOD@@ Guillermo Carrillo - v2.2 opcion 7
'--- Check incoming product pick-up location index ---  

  If bli16 < 1 Or bli16 > 42 Then
	    S11$ = S2$ + "_E1627_a"; B1 = True
	    Print "The index of the reclaim position of the incoming product is abnormal"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
  
  '--- Check incoming product grab index ---
    Print "TOOL="; Print bli17
    Tool bli17
  If bli17 < 1 Or bli17 > 3 Then
	    S11$ = S2$ + "_E1628_a"; B1 = True
	    Print "Exception in index of feeding product picking gripper"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
   

    bli15 = bli15 + bli17
    Integer num(6)
    'LoadPoints "robot1.pts"   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 1: punto de borrador
    P45 = P(bli15)
    'SavePoints "robot1.pts"
    num(0) = CX(P(bli15))
    num(1) = CY(P(bli15))
    num(2) = CU(P(bli15))
    
     Print "visual data X="; Print blr01
     Print "visual data Y="; Print blr02
     Print "visual data U="; Print blr03
   
     CX(P45) = blr01
     CY(P45) = blr02
     CU(P45) = blr03
    'SavePoints "robot1.pts"   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 1

 '---Switch tool coordinates---
  Tool bli17
  Print "zijuhao"; Print bli17
Hand P45, Lefty
Call GripperClamp(R2)
Print "Current gripper index="; Print R2
'---+20mm above the feeding point
'MOD@@ Guillermo Carrillo - v2.2 opcion 8: TomaDirecta = tomar como la Top Assy: Go directo al punto de toma con el
'  cilindro ARRIBA y luego bajarlo. Mismo punto final; se ahorra el acercamiento de 5 mm, el WaitPos
'  y la bajada lenta. La ultima parte la hace el cilindro: revisar que no golpee la PCB.
If TomaDirecta = True Then
   Go P45 /L
   Call GripperDown(R2)
Else
   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 4: con AntToma el cilindro empieza a bajar antes de llegar (!D = % del recorrido).
   If AntToma = True Then
      If R2 = 1 Then
          Go P45 +Z(5) /L !D95; Off G1Up; On G1Down!
      ElseIf R2 = 2 Then
          Go P45 +Z(5) /L !D95; Off G2Up; On G2Down!
      Else
          Go P45 +Z(5) /L !D95; Off G3Up; On G3Down!
      EndIf
   Else
      Go P45 +Z(5) /L
   EndIf
   WaitPos
   Call GripperDown(R2)
   '--- Feed product pick-up point---
   SpeedS 1000; AccelS 1000, 1000
   Move P45 /L
EndIf

If R2 = 1 Then
	B23 = True
	ElseIf R2 = 2 Then
	B24 = True
	ElseIf R2 = 3 Then
	B25 = True
EndIf
Call GripperOpen(R2)

If R2 = 1 And Sw(G1OpenSignal) = On Then
	 Pause
	ElseIf R2 = 2 And Sw(G2OpenSignal) = On Then
	 Pause
	ElseIf R2 = 3 And Sw(G3OpenSignal) = On Then
	 Pause
EndIf

SpeedS 2000; AccelS 15000, 8000
Call GripperUp(R2)


end_1:
S2$ = ""
S11$ = "010_OKOK_a"; B1 = True
Print "Robot arrives at the pick-up point of incoming products"
Tool 0
   	
Fend
Function PutDischargeDUT(bli18 As Integer, bli19 As Integer, bli20 As Integer, blr04 As Real, blr05 As Real, blr06 As Real)
''''''''''''''''''''''''''''''' Discharging product  '''''''''''''''''''''''''''''''''''''    

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Gripper 1/2/3 up 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for the gripper 1/2/3 to rise into place 

'--- Check the index of the discharge position of the discharged product  ---
Lable_14:
  If bli19 < 1 Or bli19 > 12 Then
	    S11$ = S2$ + "_E1629_a"; B1 = True
	    Print "The index of the discharge position of the discharged product is abnormal"
        Pause
	    Wait 2
	    GoTo Lable_14
  EndIf
   
'---Check the index of the discharge gripper for the discharged product  ---
Lable_15:
  If bli20 < 1 Or bli20 > 3 Then
	    S11$ = S2$ + "_E1630_a"; B1 = True
	    Print "The index of the discharging gripper of the discharging product is abnormal"
	    Pause
	    Wait 2
	    GoTo Lable_15
  EndIf

'--- Check the discharge visual data of the outgoing product ---
blr04 = 0
blr05 = 0
blr06 = 0

  Lable_19:
   	 If (blr04 < -2 Or blr04 > 2) Or (blr05 < -2 Or blr05 > 2) Or (blr06 < -2 Or blr06 > 2) Then
	      S11$ = S2$ + "_E1628_a"; B1 = True
	      Print "Outgoing product visual data is abnormal"
	      Pause
	      Wait 2
	      GoTo Lable_19
     EndIf
     Print "Visual coordinates X="; Print blr04
     Print "Visual coordinates Y="; Print blr05
     Print "Visual coordinates U="; Print blr06

  
'----Tool coordinate switch----
  Tool 0
  bli18 = bli18 + 1
  If bli20 = 1 Then
  	bli18 = bli18
  ElseIf bli20 = 2 Then
  	bli18 = bli18 + 12
  ElseIf bli20 = 3 Then
  	bli18 = bli18 + 24
  EndIf

  R10 = -60; R11 = 50; I3 = bli19; I8 = 6 'Array X Spacing; Array Y Spacing; Array Position Index; Array Rows (X direction)
  Call PalletOffset


'---Above the discharge point of the outgoing product---
Hand P(bli18), Lefty
If B21 = False Then
 Go P(bli18) +X(R12) +Y(R13) +Z(20) +U(blr06) /L
Else
 Go P(bli18) +X(R12) +Y(R13) +Z(1.5) +U(blr06) /L
EndIf

Call GripperDown(R2)


SpeedS 1000; AccelS 1000, 1000

If B21 = False Then
 Move P(bli18) +X(R12) +Y(R13) +Z(20) +U(blr06) /L Till Sw(75 + R2) = On '+1.5mm above the discharge point of the discharged product 
Else
 Move P(bli18) +X(R12) +Y(R13) +U(blr06) /L Till Sw(75 + R2) = On '+1.5mm above the discharge point of the discharged product 
EndIf

If B21 = False Then
 Move P(bli18) +X(R12) +Y(R13) +Z(20) +U(blr06) /L 'Discharge point of discharge product 
Else
 Move P(bli18) +X(R12) +Y(R13) +U(blr06) /L 'Discharge point of discharge product 
EndIf


Call GripperClamp(R2)

'---+20mm above the discharge point of the discharged product
Wait 0.2
SpeedS 1000; AccelS 1000, 1000
If B21 = False Then
 Move P(bli18) +X(R12) +Y(R13) +Z(20) +U(blr06) /L
Else
 Move P(bli18) +X(R12) +Y(R13) +U(blr06) /L
EndIf

Call GripperUp(R2)
S2$ = ""
S11$ = "011_OKOK_a"; B1 = True
Print "The robot reaches the discharge point for the outgoing product"


Lable_21:
S2$ = ""
SpeedS 2000; AccelS 10000, 8000
Tool 0

Fend

Function PutDischargeDUT_2(bli18 As Integer, bli19 As Integer, bli20 As Integer)

'''''''''''''''''''''''''''''''Discharging product discharging  '''''''''''''''''''''''''''''''''''''    

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Grip 1/2/3 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for gripper 1/2/3 to rise into position
  Accel AccelTrabajo, AccelTrabajo   'MOD@@ Guillermo Carrillo - v2.2 opcion 7 (la bajada con sensor no cambia)


'--- Check discharging position index of discharging products ---
  If bli19 < 1 Or bli19 > 12 Then
  		S2$ = ""
	    S11$ = S2$ + "_E1629_a"; B1 = True
	    Print "Exception in discharging position index of discharging product"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
   Print "Current position index ="
   Print bli19
   
'---Check the index of discharging gripper for discharging products  ---
  If bli20 < 1 Or bli20 > 3 Then
  		S2$ = ""
	    S11$ = S2$ + "_E1630_a"; B1 = True
	    Print "Exception in index of discharging gripper of discharging product"
	    Pause
	   S2$ = ""
	   Exit Function
  EndIf
  Tool 0
  If bli20 = 2 Then
  	bli18 = bli18 + 12
  ElseIf bli20 = 3 Then
  	bli18 = bli18 + 24
  EndIf
  
  bli18 = bli18 + bli19
  'LoadPoints "robot1.pts"   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 1: punto de borrador
  P46 = P(bli18)
  'SavePoints "robot1.pts"

'---Switch tool coordinates---

'Print "Destination coordinates:" + Str$(CX(P46)) + "," + Str$(CY(P46)) + "," + Str$(CZ(P46)) + "," + Str$(CU(P46))
Hand P46, Lefty
'MOD@@ Guillermo Carrillo - v2.1.0 opcion 4: con AntColoca el cilindro empieza a bajar antes de llegar (!D = % del recorrido).
If AntColoca = True Then
   If R2 = 1 Then
       Go P46 +Z(3) /L !D98; Off G1Up; On G1Down!
   ElseIf R2 = 2 Then
       Go P46 +Z(3) /L !D97; Off G2Up; On G2Down!
   Else
       Go P46 +Z(3) /L !D97; Off G3Up; On G3Down!
   EndIf
Else
   Go P46 +Z(3) /L
EndIf
'Print "Arrived to destination coordinates + Z 3mm: " + Str$(CX(Here)) + "," + Str$(CY(Here)) + "," + Str$(CZ(Here)) + "," + Str$(CU(Here))
Call GripperDown(R2)

SpeedS 1000; AccelS 1000, 1000


If R2 = 1 Then
	B23 = False
	ElseIf R2 = 2 Then
	B24 = False
	ElseIf R2 = 3 Then
	B25 = False
EndIf

'---Discharging point for outgoing products---

'MOD@@ Guillermo Carrillo - v2.1.0 opcion 2: el robot ya esta en P46 +Z(3): ese Move no movia nada.
'Move P46 +Z(3) /L Till Sw(75 + R2) = On  '+4.5mm above the discharge point of the discharged product 
'Wait 0.1
Wait 0.05
'Print "Moved 3mm above"

'If Sw(75 + R2) = On Then
'   Print "UP ,The Discharge pressure is too large, and the robot has stopped discharge and moved up."
'   GoTo Lable_21
'EndIf

Move P46 /L 'Discharge point of discharge product 

'Print "Moved to desination coordinates:" + Str$(CX(Here)) + "," + Str$(CY(Here)) + "," + Str$(CZ(Here)) + "," + Str$(CU(Here))

Wait Sw(75 + R2) = On, 0.5
If TW = True Then
Go Here +Z(-1.0)
   Wait Sw(75 + R2) = On, 0.3
   'Print "Pressure successful  attempt 2"
   If TW = True Then
      Print "DOWN ,TheDeviation of discharge position, and the robot has stopped discharge and moved up."
      GoTo Lable_21
   EndIf
EndIf



Call GripperClamp(R2)
'Print "pressure successful at:" + Str$(CX(Here)) + "," + Str$(CY(Here)) + "," + Str$(CZ(Here)) + "," + Str$(CU(Here))

Call GripperUp(R2)
Print "Gripper up"
S2$ = ""
S11$ = "011_OKOK_a"; B1 = True
Print "Robot reaches the discharging point of discharging product"
SpeedS 2000; AccelS 15000, 8000   'MOD@@ Guillermo Carrillo - v2.2 opcion 9 (era AccelS 1000, 1000: descuido del proveedor)
GoTo Lable_22

Lable_21:
    S2$ = ""
   
	If R2 = 1 Then
		 S11$ = "011_E1642_a"; B1 = True
	ElseIf R2 = 2 Then
		 S11$ = "011_E1643_a"; B1 = True
	ElseIf R2 = 3 Then
		 S11$ = "011_E1644_a"; B1 = True
	EndIf
    GoTo Lable_24

Lable_24:
     Move Here /L +Z(0) 'Feeding and taking point of product 
    Call GripperUp(R2)
    SpeedS 2000; AccelS 15000, 8000
Lable_22:

   Tool 0

Fend
Function PutDischargeDUT_1(bli18 As Integer, bli19 As Integer, bli20 As Integer, blr04 As Real, blr05 As Real, blr06 As Real)

'''''''''''''''''''''''''''''''Discharging product discharging  '''''''''''''''''''''''''''''''''''''    

  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up 'Grip 1/2/3 
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On 'Wait for gripper 1/2/3 to rise into position
  Tool 0
  
  
     bli18 = bli18 + bli20
    Integer num(6)
    LoadPoints "robot1.pts"
    P46 = P(bli18)
    SavePoints "robot1.pts"
    num(0) = CX(P(bli18))
    num(1) = CY(P(bli18))
    num(2) = CU(P(bli18))
    
    CX(P46) = blr04
    CY(P46) = blr05
    CU(P46) = blr06
    SavePoints "robot1.pts"
    Print "visual data X="; Print blr04
    Print "visual data Y="; Print blr05
    Print "visual data U="; Print blr06


'--- Check discharging position index of discharging products ---
Lable_14:
  If bli19 < 1 Or bli19 > 12 Then
  		S2$ = ""
	    S11$ = S2$ + "_E1629_a"; B1 = True
	    Print "Exception in discharging position index of discharging product"
	    Pause
	    Wait 2
	    GoTo Lable_14
  EndIf
   Print "Current position index ="
   Print bli19
   
'---Check the index of discharging gripper for discharging products  ---
Lable_15:
  If bli20 < 1 Or bli20 > 3 Then
  		S2$ = ""
	    S11$ = S2$ + "_E1630_a"; B1 = True
	    Print "Exception in index of discharging gripper of discharging product"
	    Pause
	    Wait 2
	    GoTo Lable_15
  EndIf

  '--- Get the current robot position  ---
  LoadPoints "robot1.pts"
  P99 = CurPos
  SavePoints "robot1.pts"
  '--- Robot motion transition ---
'  If CZ(P99) < CZ(P46) + 20 - 2 Then
'     CZ(P99) = CZ(P46) + 20
'     Move P99
'  EndIf
  If CY(P99) <= 130 Then
     Go P0 CP
  EndIf

'---Switch tool coordinates---
  Tool 0
  
Hand P46, Lefty

'---+20mm above discharging point of discharging product

Go P46 +Z(4.5) /L
SpeedS 500; AccelS 500, 500
WaitPos
Call GripperDown(R2)

If R2 = 1 Then
	B23 = False
	ElseIf R2 = 2 Then
	B24 = False
	ElseIf R2 = 3 Then
	B25 = False
EndIf

'---Discharging point for outgoing products---

Move P46 /L +Z(4.5) /L Till Sw(75 + R2) = On  '+4.5mm above the discharge point of the discharged product 
Wait 0.2

If Sw(75 + R2) = On Then
   Print "UP ,The Discharge pressure is too large, and the robot has stopped discharge and moved up."

   GoTo Lable_21
EndIf
Move P46 /L 'Discharge point of discharge product 

Wait Sw(75 + R2) = On, 0.3
If TW = True Then
Go Here +Z(-0.3)
   Wait Sw(75 + R2) = On, 0.3
   If TW = True Then
      Print "DOWN ,TheDeviation of discharge position, and the robot has stopped discharge and moved up."
      GoTo Lable_21
   EndIf
EndIf

''Discharging point of discharging product
'SpeedS 200; AccelS 2000, 1000
'Move P46 /L
'WaitPos

'Gripper to open
Wait 0.1
Call GripperClamp(R2)

'---+20mm above discharging point of discharging product
'Wait 0.3
SpeedS 1000; AccelS 1000, 1000
Move P46 +Z(0) /L

'Gripper rises
Call GripperUp(R2)


S2$ = ""
S11$ = "011_OKOK_a"; B1 = True
Print "Robot reaches the discharging point of discharging product"
SpeedS 2000; AccelS 15000, 8000
GoTo Lable_22

Lable_21:

    S2$ = ""
   
	If R2 = 1 Then
		 S11$ = "011_E1642_a"; B1 = True
	ElseIf R2 = 2 Then
		 S11$ = "011_E1643_a"; B1 = True
	ElseIf R2 = 3 Then
		 S11$ = "011_E1644_a"; B1 = True
	EndIf
    GoTo Lable_24

Lable_24:
     Move Here /L +Z(0) 'Feeding and taking point of product 
    Call GripperUp(R2)
    SpeedS 2000; AccelS 15000, 8000
Lable_22:
  SpeedS 2000; AccelS 15000, 8000
   Tool 0

Fend

Function GetCurrentPosition(bli28 As Integer)
''''''''''''''''''''''''''''''' Get robot position '''''''''''''''''''''''''''''''''''''    
  LoadPoints "robot1.pts"
R15 = CX(P(bli28))
R16 = CY(P(bli28))
R17 = CZ(P(bli28))
R18 = CU(P(bli28))
S15$ = Str$(R15)
S16$ = Str$(R16)
S17$ = Str$(R17)
S18$ = Str$(R18)
S13$ = "_" + S15$ + "_" + S16$ + "_" + S18$
S11$ = S2$ + S13$ + "_OKOK_a"; B1 = True
   
Fend
Function GetCurrentPosition_1
''''''''''''''''''''''''''''''' Get robot position '''''''''''''''''''''''''''''''''''''    
  If I21 > 0 And I21 < 4 Then
  	Tool I21
  	GoTo pos_1
  EndIf
  Tool 0

pos_1:
  I21 = 0
  'LoadPoints "robot1.pts"   'MOD@@ Guillermo Carrillo - v2.1.0 opcion 1: punto de borrador
  P99 = CurPos
  'SavePoints "robot1.pts"
R15 = CX(P99)
R16 = CY(P99)
R17 = CZ(P99)
R18 = CU(P99)
S15$ = Str$(R15)
S16$ = Str$(R16)
S17$ = Str$(R17)
S18$ = Str$(R18)
S13$ = "_" + S15$ + "_" + S16$ + "_" + S18$
S11$ = S2$ + S13$ + "_OKOK_a"; B1 = True
  S2$ = ""
Fend
Function BackSafe
''''''''''''''''''''''''''''''' Robot returns to safety point '''''''''''''''''''''''''''''''''''''    
  
 If B20 = True Then
  '--- Check whether the robot gripper has a product --- 
   Lable_4:
        If Sw(G1OpenSignal) = On Then
      		On G1Clamp; Off G1Open
      	EndIf
      	
      	If Sw(G2OpenSignal) = On Then
      		On G2Clamp; Off G2Open
      	EndIf
      	
      	If Sw(G3OpenSignal) = On Then
      		On G3Clamp; Off G3Open
      	EndIf
      	

    If Sw(G1OpenSignal) = Off Or Sw(G2OpenSignal) = Off Or Sw(G3OpenSignal) = Off Then
       Wait 0.1
       If Sw(G1OpenSignal) = Off And Sw(G1ClampSignal) = Off Then
       	  S11$ = "013_E9000_a"; B1 = True
       	  Print "Robot initialization gripper 1 has a product exception"
	      'Pause
	      Print B22
	      If B22 = True Then
	         GoTo Lable_19
	      Else
	         GoTo Lable_18
	      EndIf
	    
	   ElseIf Sw(G1OpenSignal) = Off And Sw(G1ClampSignal) = On And Oport(G1Clamp) = Off Then
	   	  S11$ = "013_E9000_a"; B1 = True
       	  Print "Robot initialization gripper 1 has a product exception / Sensor exception"
	      'Pause
	      Print B22
	      If B22 = True Then
	         GoTo Lable_19
	      Else
	         GoTo Lable_18
	      EndIf
	   Else
	   	On G1Clamp; Off G1Open
	      
       EndIf
       If Sw(G2OpenSignal) = Off And Sw(G2ClampSignal) = Off Then
       	  S11$ = "013_E9001_a"; B1 = True
       	  Print "Robot initialization gripper 2 has a product exception"
	      'Pause
	      Print B22
	      If B22 = True Then
	      	 GoTo Lable_19
	      Else
	      	 GoTo Lable_18
	      EndIf
	      
	    ElseIf Sw(G2OpenSignal) = Off And Sw(G2ClampSignal) = On And Oport(G2Clamp) = Off Then
	   	  S11$ = "013_E9001_a"; B1 = True
       	  Print "Robot initialization gripper 2 has a product exception / Sensor exception"
	      'Pause
	      Print B22
	      If B22 = True Then
	         GoTo Lable_19
	      Else
	         GoTo Lable_18
	      EndIf
	   	Else
	     	On G2Clamp; Off G2Open
       EndIf
       If Sw(G3OpenSignal) = Off And Sw(G3ClampSignal) = Off Then
       	  S11$ = "013_E9002_a"; B1 = True
       	  Print "Robot initialization gripper 3 has a product exception"
	      'Pause
	      Print B22
	      If B22 = True Then
	      	 GoTo Lable_19
	      Else
	      	 GoTo Lable_18
	      EndIf
	      
	   ElseIf Sw(G3OpenSignal) = Off And Sw(G3ClampSignal) = On And Oport(G3Clamp) = Off Then
	   	  S11$ = "013_E9002_a"; B1 = True
       	  Print "Robot initialization gripper 3 has a product exception / Sensor exception"
	      'Pause
	      Print B22
	      If B22 = True Then
	         GoTo Lable_19
	      Else
	         GoTo Lable_18
	      EndIf
       Else
	     	On G3Clamp; Off G3Open
       EndIf
      
    Else
        B22 = False
        On G1Clamp; On G2Clamp; On G3Clamp; Off G1Open; Off G2Open; Off G3Open;
    EndIf
   Else
        B22 = False
        On G1Clamp; On G2Clamp; On G3Clamp; Off G1Open; Off G2Open; Off G3Open;
   EndIf

   
Lable_19:
   
SpeedFactor 30
Tool 0
I2 = 0

'--- Robot attitude confirmation error proofing ---  
Lable_5:
  If Hand(Here) = Righty Then
     Wait 2
     If Hand(Here) = Righty Then
	    S11$ = "013_E1613_a"; B1 = True
	    Print "The robot poses abnormally"
	    Pause
	    GoTo Lable_5
	 EndIf
  EndIf

'--- Judge the status of the 3rd axis and lock it ---
If SFree(3) = True Then
   SLock 3
EndIf

  LoadPoints "robot1.pts"
  P99 = CurPos
  If CZ(P0) <= -2 And CZ(P0) > 0 Then   'Determine the height of the robot safety bit
  CZ(P0) = -1
  EndIf
  SavePoints "robot1.pts"
  
  If CZ(P99) < CZ(P0) Then             'Determine the height of the current position of the robot
  CZ(P99) = CZ(P0)
  Move P99
  ElseIf CZ(P99) > 0 Then
  S11$ = "013_E1612_a"; B1 = True
  Print "The height of the 3rd axis of the robot is abnormal"
  Pause
  EndIf
  
  Go P0
  
  If B22 = True Then
     B22 = False
     Print B22
     If B20 = False Then
        S2$ = ""
     	S11$ = "013_OKOK_a"; B1 = True
     EndIf
  Else
  	  S2$ = ""
     S11$ = "013_OKOK_a"; B1 = True
  EndIf
  Print "The robot reaches the safe point"
  Print "Robot reaches safe point"
  Lable_18:
  SpeedFactor I1
  Tool 0
  
Fend
Function VisionCalibration(bli22 As Integer, bra11 As Real, bra12 As Real, bra13 As Real)
''''''''''''''''''''''''''''''' visual calibration '''''''''''''''''''''''''''''''''''''    

'--- Check Vision Calibration Location Index ---
Lable_16:
  If bra11 < -15 Or bra11 > 15 Or bra12 < -15 Or bra12 > 15 Or bra13 < -15 Or bra13 > 15 Then
        Wait 1
	    S11$ = S2$ + "_E1637_a"; B1 = True
	    Print "Visual calibration position index abnormal"
	    Pause
	    GoTo Lable_16
  EndIf
  LoadPoints "robot1.pts"
  P99 = CurPos
  SavePoints "robot1.pts"

Select bli22
	 Case 5
	 	Print "The current moving camera position is="; Print bli22
	  	I20 = 5
	  If CZ(P99) < -50 Then
	     CZ(P99) = -50
	     Move P99
	  EndIf
	  If CY(P99) > 130 Then
	     Go P0 CP
	  EndIf
	  Go P(bli22)
  	 
	Case 11
		Print "The current moving camera position is="; Print bli22
	    I20 = 11
	  If CZ(P99) < -30 Then
	     CZ(P99) = -30
	     Move P99
	  EndIf
	  If CY(P99) < 130 Then
	     Go P0 CP
	  EndIf
	  Go P(bli22)
	 	  
	Default
	  	
	  If I20 = 0 Then
	  	GoTo label_aa
	  EndIf
	  Go P(I20) +X(bra11) +Y(bra12) +U(bra13) /L 'Vision Calibration Teach Points
	  WaitPos
  
Send

label_aa:
	S19$ = S2$
	LoadPoints "robot1.pts"
	P98 = CurPos
	SavePoints "robot1.pts"
	R15 = CX(P98)
	R16 = CY(P98)
	R17 = CZ(P98)
	R18 = CU(P98)
	S15$ = Str$(R15)
	S16$ = Str$(R16)
	S17$ = Str$(R17)
	S18$ = Str$(R18)
	S13$ = "_" + S15$ + "_" + S16$ + "_" + S18$
	S2$ = ""
	S11$ = S19$ + S13$ + "_OKOK_a"; B1 = True
      
Fend
Function RecordCurrentPosition(bli23 As Integer)
''''''''''''''''''''''''''''''' Record the current position of the robot '''''''''''''''''''''''''''''''''''''    

'--- Check record robot position index ---
Lable_17:
  If bli23 < 900 Or bli23 > 999 Then
  	 Wait 2
     If bli23 < 900 Or bli23 > 999 Then
        S2$ = ""
	    S11$ = S2$ + "_E054_a"; B1 = True
	    Print "Record robot position index exception"
	    Pause
	    GoTo Lable_17
	 EndIf
  EndIf

If bli23 >= 900 Then
    LoadPoints "robot1.pts"
	P99 = CurPos
	CX(P(bli23)) = CX(P99)
	CY(P(bli23)) = CY(P99)
	CZ(P(bli23)) = CZ(P99)
	CU(P(bli23)) = CU(P99)
	SavePoints "robot1.pts"
	S11$ = S2$ + "_OKOK_a"; B1 = True
EndIf
   
Fend
Function WriteRobotDO
''''''''''''''''''''''''''''''' Write Robot General Output '''''''''''''''''''''''''''''''''''''    

Integer d
For d = 1 To 7
	S20$(d) = Mid$(S3$(3), d, 1)
	S20$(d + 7) = Mid$(S3$(4), d, 1)
	S20$(d + 14) = Mid$(S3$(5), d, 1)
Next
For d = 1 To 16
	R20(d) = Val(S20$(d))
	If R20(d) = 1 Then
	On (d + 63)
	EndIf
	If R20(d) = 0 Then
	Off (d + 63)
	EndIf
	
Next
S11$ = S2$ + "_OKOK_a"; B1 = True

Fend
Function ReadRobotDO
'''''''''''''''''''''''''''''''Read Robot Universal Output  '''''''''''''''''''''''''''''''''''''   
   
String sta_DO$(20), sta_read_DO$(2), c1$(8), c2$(8)
Integer g, g1, sta0(8), sta1(8), aa(8), bb(8)
sta0(0) = Out(8)
sta1(0) = Out(9)
For g = 0 To 7
	aa(g) = sta0(g) Mod 2
	sta0(g + 1) = sta0(g) / 2
	If sta0(g) = 1 Then
        GoTo end_for
        EndIf
Next
end_for:
For g = 0 To 7
    c1$(g) = Str$(aa(g))
Next
sta_read_DO$(1) = c1$(0) + c1$(1) + c1$(2) + c1$(3) + c1$(4) + c1$(5) + c1$(6) + c1$(7)

For g = 0 To 7
	bb(g) = sta1(g) Mod 2
	sta1(g + 1) = sta1(g) / 2
	If sta1(g) = 1 Then
        GoTo end_for2
        EndIf
Next
end_for2:
For g = 0 To 7
    c2$(g) = Str$(bb(g))
Next
sta_read_DO$(2) = c2$(0) + c2$(1) + c2$(2) + c2$(3) + c2$(4) + c2$(5) + c2$(6) + c2$(7)
S13$ = "_" + sta_read_DO$(1) + sta_read_DO$(2)
S11$ = S2$ + S13$ + "_OKOK_a"; B1 = True
   
Fend
Function ReadRobotDI
''''''''''''''''''''''''''''''' Read Robot Universal Input '''''''''''''''''''''''''''''''''''''   
   
String sta_di$(20), sta_read_di$(2), e1$(8), e2$(8)
Integer f, f1, sta_di0(8), sta_di1(8), aaa(8), bbb(8)
sta_di0(0) = In(8)
sta_di1(0) = In(9)
For f = 0 To 7
	aaa(f) = sta_di0(f) Mod 2
	sta_di0(f + 1) = sta_di0(f) / 2
	If sta_di0(f) = 1 Then
        GoTo aaa_end
        EndIf
Next
aaa_end:
For f = 0 To 7
    e1$(f) = Str$(aaa(f))
Next
sta_read_di$(1) = e1$(0) + e1$(1) + e1$(2) + e1$(3) + e1$(4) + e1$(5) + e1$(6) + e1$(7)

For f = 0 To 7
	bbb(f) = sta_di1(f) Mod 2
	sta_di1(f + 1) = sta_di1(f) / 2
	If sta_di1(f) = 1 Then
        GoTo bbb_end
        EndIf
Next
bbb_end:
For f = 0 To 7
    e2$(f) = Str$(bbb(f))
Next
sta_read_di$(2) = e2$(0) + e2$(1) + e2$(2) + e2$(3) + e2$(4) + e2$(5) + e2$(6) + e2$(7)
S13$ = "_" + sta_read_di$(1) + sta_read_di$(2)
S11$ = S2$ + S13$ + "_OKOK_a"; B1 = True
      
Fend
Function GripperClamp(bli24 As Integer)
''''''''''''''''''''''''''''''' Gripper clamping '''''''''''''''''''''''''''''''''''''   
   
Select bli24
  Case 1
  	Off G1Open
  	On G1Clamp 'Gripper 1 clamping
  	Wait Sw(G1ClampSignal) = On, 2 'Wait for gripper 1 to be clamped in place
  Case 2
  	Off G2Open
  	On G2Clamp 'Gripper 2 clamping
  	Wait Sw(G2ClampSignal) = On, 2 'Wait for gripper 2 to be clamped in place  	
  Case 3
  	Off G3Open
  	On G3Clamp 'Gripper 3 clamping
    Wait Sw(G3ClampSignal) = On, 2 'Wait for gripper 3 to be clamped in place 	
  Default
Send
      
Fend
Function GripperOpen(bli25 As Integer)
''''''''''''''''''''''''''''''' open hand '''''''''''''''''''''''''''''''''''''    

Select bli25
  Case 1
  	Off G1Clamp
  	On G1Open '1 open handle
  	Wait 0.2
  	'Wait Sw(G1OpenSignal) = Off 'Wait for gripper 1 to open in place
  Case 2
  	Off G2Clamp
  	On G2Open '2 open handle
  	Wait 0.2
  	'Wait Sw(G2OpenSignal) = Off 'Wait for gripper 2 to open in place 	
  Case 3
  	Off G3Clamp
  	On G3Open '3 open handle
  	Wait 0.2
  	'Wait Sw(G3OpenSignal) = Off 'Wait for gripper 3 to open in place
  Default
Send
   
Fend
Function GripperUp(bli26 As Integer)
''''''''''''''''''''''''''''''' Gripper rises '''''''''''''''''''''''''''''''''''''   
   
Select bli26
  Case 1
  	Off G1Down
  	On G1Up 'Gripper 1 rises
  	Wait Sw(G1UpSignal) = On, 2 'Wait for gripper 1 to rise into place
  Case 2
  	Off G2Down
  	On G2Up 'Gripper 2 rises
  	Wait Sw(G2UpSignal) = On, 2 'Wait for gripper 2 to rise into place  	
  Case 3
  	Off G3Down
  	On G3Up 'Gripper 3 rises
  	Wait Sw(G3UpSignal) = On, 2 'Wait for gripper 3 to rise into place  	
  Default
Send
   
Fend
Function GripperDown(bli27 As Integer)
''''''''''''''''''''''''''''''' Gripper drops '''''''''''''''''''''''''''''''''''''   
   
Select bli27
  Case 1
  	Off G1Up
  	On G1Down 'Gripper 1 drop
  	Wait Sw(G1DownSignal) = On, 2 'Wait for gripper 1 to drop into place
  Case 2
  	Off G2Up
  	On G2Down 'Gripper 2 drop
  	Wait Sw(G2DownSignal) = On, 2 'Wait for gripper 2 to drop into place  	
  Case 3
  	Off G3Up
  	On G3Down 'Gripper 3 drop
  	Wait Sw(G3DownSignal) = On, 2 'Wait for gripper 3 to drop into place	
  Default
Send

Fend
Function PalletOffset
''''''''''''''''''''''''''''''' Array X/Y Offset Calculation '''''''''''''''''''''''''''''''''''''   

Integer bli100, bli101, bli102, bli103, bli104
bli104 = I8 'Number of array rows (X direction)
bli100 = I3
bli101 = I3
bli100 = bli100 - 1
bli101 = bli101 - 1
bli100 = bli100 / bli104
bli102 = bli100
bli102 = bli102 * bli104
bli101 = bli101 - bli102
R12 = R10
R12 = R12 * bli101 'Calculate array X-axis offset
R13 = R11
R13 = R13 * bli100 'Calculate array Y-axis offset
Print "Array X/Y Offset="; Print R12, R13

Fend
Function StartTask
'''''''''''''''''''''''''''''''' Start background tasks '''''''''''''''''''''''''''''''''''''   
'
'Do
''--- Determine the status of background tasks and start background tasks ---
' If TaskState(PCNET) = 0 Then
'	Xqt 66, PCnet, NoEmgAbort
' ElseIf TaskState(ManualMode) = 0 Then
'	Xqt 67, ManualMode
' ElseIf TaskState(MonitorIO) = 0 Then
'    Xqt 68, MonitorIO
' EndIf
'Loop
'     
Fend
Function MonitorIO
''''''''''''''''''''''''''''''' IO monitoring and alarm '''''''''''''''''''''''''''''''''''''   
   
Do
Wait 0.2
'--- Gripper 1 Gripper Cylinder Clamping Signal Monitoring ---
If Oport(G1Clamp) = On Then
   Wait Sw(G1ClampSignal) = On, 0.2
   If Oport(G1Clamp) = On And Sw(G1ClampSignal) = Off Then
      If S2$ <> "" Then
         S11$ = S2$ + "_E1600_a"; B1 = True
         Print "Gripper 1 Clamping Clamp Cylinder Clamping Signal Abnormal"
         Pause
      EndIf
   EndIf
EndIf

'--- Gripper 1 Gripper Cylinder Release Signal Monitoring ---
If Oport(G1Open) = On And B23 = True Then
   Wait Sw(G1OpenSignal) = On, 0.2
   If Oport(G1Open) = On And Sw(G1OpenSignal) = On Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1601_a"; B1 = True
      Print "Gripper 1 Gripper Cylinder Release Signal Abnormal"
      B23 = False
      EndIf
   EndIf
EndIf

'--- Gripper 2 gripper cylinder clamping signal monitoring  ---
If Oport(G2Clamp) = On Then
   Wait Sw(G2ClampSignal) = On, 0.2
   If Oport(G2Clamp) = On And Sw(G2ClampSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1602_a"; B1 = True
      Print "The gripper 2 gripper cylinder clamping signal is abnormal"
      Pause
      EndIf
   EndIf
EndIf

'--- Grip 2 claw cylinder release signal monitoring ---
If Oport(G2Open) = On And B24 = True Then
   Wait Sw(G2OpenSignal) = On, 0.2
   If Oport(G2Open) = On And Sw(G2OpenSignal) = On Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1603_a"; B1 = True
      Print "mGrip 2 Claw cylinder release signal is abnormal"
      B24 = False
      EndIf
   EndIf
EndIf

'--- Gripper 3 gripper cylinder clamping signal monitoring ---
If Oport(G3Clamp) = On Then
   Wait Sw(G3ClampSignal) = On, 0.2
   If Oport(G3Clamp) = On And Sw(G3ClampSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1604_a"; B1 = True
      Print "Gripper 3 gripper cylinder clamping signal abnormal"
      Pause
      EndIf
   EndIf
EndIf

'--- Grip 3 claw cylinder release signal monitoring ---
If Oport(G3Open) = On And B25 = True Then
   Wait Sw(G3OpenSignal) = On, 0.2
   If Oport(G3Open) = On And Sw(G3OpenSignal) = On Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1605_a"; B1 = True
      Print "Grip 3 The release signal of the claw cylinder is abnormal"
      B25 = False
      EndIf
   EndIf
EndIf

'--- Gripper 1 lift cylinder rising signal monitoring  ---
If Oport(G1Up) = On Then
   Wait Sw(G1UpSignal) = On, 1
   If Oport(G1Up) = On And Sw(G1UpSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1606_a"; B1 = True
      Print "Gripper 1 lift cylinder rising signal abnormal"
      Pause
      EndIf
   EndIf
EndIf

'--- Gripper 1 lifting cylinder lowering signal monitoring  ---
If Oport(G1Down) = On Then
   Wait Sw(G1DownSignal) = On, 1
   If Oport(G1Down) = On And Sw(G1DownSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1607_a"; B1 = True
      Print "Abnormal lowering signal of gripper 1 lifting cylinder"
      Pause
      EndIf
   EndIf
EndIf

'--- Gripper 2 lift cylinder rising signal monitoring  ---
If Oport(G2Up) = On Then
   Wait Sw(G2UpSignal) = On, 1
   If Oport(G2Up) = On And Sw(G2UpSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1608_a"; B1 = True
      Print "Gripper 2 lift cylinder rising signal abnormal"
      Pause
      EndIf
   EndIf
EndIf

'--- Gripper 2 lift cylinder lowering signal monitoring  ---
If Oport(G2Down) = On Then
   Wait Sw(G2DownSignal) = On, 1
   If Oport(G2Down) = On And Sw(G2DownSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1609_a"; B1 = True
      Print "Abnormal lowering signal of gripper 2 lift cylinder"
      Pause
      EndIf
   EndIf
EndIf

'--- Gripper 3 lift cylinder rise signal monitoring  ---
If Oport(G3Up) = On Then
   Wait Sw(G3UpSignal) = On, 1
   If Oport(G3Up) = On And Sw(G3UpSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1610_a"; B1 = True
      Print "Gripper 3 lift cylinder rising signal abnormal"
      Pause
      EndIf
   EndIf
EndIf

'--- Gripper 3 lifting cylinder lowering signal monitoring  ---
If Oport(G3Down) = On Then
   Wait Sw(G3DownSignal) = On, 1
   If Oport(G3Down) = On And Sw(G3DownSignal) = Off Then
      If S2$ <> "" Then
      S11$ = S2$ + "_E1611_a"; B1 = True
      Print "Abnormal lowering signal of gripper 3 lift cylinder"
      Pause
      EndIf
   EndIf
EndIf
   
   If B19 = True Then
   	B19 = False
   	Pause
   EndIf
   
   
   
Loop

Fend


Function bgmain
'''''''''''''''''''''''''''''''' Start background tasks  ''''''''''''''''''''''''''''''''''''' 

Do

'--- Determine the status of background tasks and start background tasks  ---
' If TaskState(PCNET) = 0 Or TaskState(PCNET) = 5 Then
'	'Xqt 66, PCnet, NoEmgAbort
' EndIf
 If TaskState(MonitorIO) = 0 Or TaskState(MonitorIO) = 5 Then
    Xqt 68, MonitorIO
 EndIf

'---------Connect PC -------- 
again_1:
   Print "bgmain is beginning"
   Call PCnet
'   Wait 0.01
GoTo again_1

Loop

Fend
Function PCnet
''''''''''''''''''''''''''''''' Communicate with the host computer software  '''''''''''''''''''''''''''''''''''''
   S10$ = ""
   CloseNet #201
   Wait 0.8
   OpenNet #201 As Server
   Print "waiting for connect....."
   WaitNet #201
   Print "connect Success....."
   
Do
   If ChkNet(201) < -3 Then
   Print "check the NET fail....."
   Exit Do
   EndIf
   
   '---------recevie From PC---------
   If ChkNet(201) > 0 Then
   Input #201, S10$
   Print "receive from PC=" + S10$
   Print #201, S10$
   Print "send to PC=" + S10$
   
   Call Message_analyze
   EndIf
          
   
'---------Send to PC-------------     
   If B1 = True And S11$ <> "" Then
      B1 = False
      Print #201, S11$
      Print S11$
      
      S10$ = ""
      S3$(0) = ""
      S12$ = ""
      S13$ = ""
      S11$ = ""
   EndIf
             
  
Loop


Fend
Function Message_analyze
	
	   '---------parsing strings---------    
   S3$(0) = Left$(S10$, 3)
   S3$(1) = Mid$(S10$, 5, 3)
   S3$(2) = Mid$(S10$, 9, 2)
   S3$(3) = Mid$(S10$, 12, 7)
   S3$(4) = Mid$(S10$, 20, 7)
   S3$(5) = Mid$(S10$, 28, 7)
   S3$(6) = Right$(S10$, 1)
   
   S3$(7) = Mid$(S10$, 4, 1)
   S3$(8) = Mid$(S10$, 8, 1)
   S3$(9) = Mid$(S10$, 11, 1)
   S3$(10) = Mid$(S10$, 19, 1)
   S3$(11) = Mid$(S10$, 27, 1)
   S3$(12) = Mid$(S10$, 35, 1)

 If S3$(0) = "101" Then
 	Call WriteRobotDO
 GoTo Lable_3
 EndIf
 
  If S3$(0) = "102" Then
Call ReadRobotDO
 GoTo Lable_3
 EndIf
 
  If S3$(0) = "103" Then
 	Call ReadRobotDI
 GoTo Lable_3
 EndIf

   If S3$(0) = "104" Then
      R21 = Val(S3$(3))
      Call WriteRobotSpeed
   Else
	  R1 = Val(S3$(1))
	  R2 = Val(S3$(2))
	  R3 = Val(S3$(3))
	  R4 = Val(S3$(4))
	  R5 = Val(S3$(5))
	  S2$ = S3$(0)
   EndIf
'   Wait 0.01
   
	'---------check function code--------
   I5 = Val(S3$(0))
   If I5 <= 0 Then
   Print "function code is " + S3$(0)
   S11$ = S2$ + "_E1619_a"; B1 = True
   Pause
   B1 = True
   B19 = True
   Wait 0.5
   GoTo Lable_3
   EndIf
   
   '---------check string num--------    
   I6 = Len(S10$)
   If I6 <> 36 Then
   Print "Not enough strings....."
   S11$ = S2$ + "_E1618_a"; B1 = True
   Pause
   B1 = True
   B19 = True
   Wait 0.2
   GoTo Lable_3
   EndIf
   '---------check the end string-----   	
   S4$ = Mid$(S10$, 36, 1)
   S5$ = Right$(S10$, 1)
   If S4$ <> "a" Or S5$ <> "a" Then
   Print "check the end string is error....."
   S11$ = S2$ + "_E1621_a"; B1 = True
   Pause
   B1 = True
   B19 = True
   Wait 0.2
   GoTo Lable_3
   EndIf
   '---------check separator-----   	
   Integer a
   For a = 7 To 11
   	  If S3$(a) <> S3$(a + 1) Then
   Print "check the separator....."
   S11$ = S2$ + "_E1622_a"; B1 = True
   Pause
   B1 = True
   B19 = True
   Wait 0.2
   GoTo Lable_3
      EndIf
   Next
	  
			
   '--------- data analysis---------       
   Select S2$
    Case "012"  'Get robot position 
    	Call GetCurrentPosition(R1)
    	S2$ = ""
    Case "105"  'Read Robot Speed  
    	Call ReadRobotSpeed
    	S2$ = ""
    Case "018"  'visual switch
    	Call photo_change(R1)
    	S2$ = ""
    Send
     

Lable_3:
 S3$(0) = ""

Fend
Function photo_change(bli67 As Integer)

Label667:
	If bli67 < 1 Or bli67 > 3 Then
	S11$ = S2$ + "_E1620_a"; B1 = True
	Wait 0.01
	  Pause
	  GoTo Label667
	EndIf
	
	If bli67 = 1 Then
		B17 = True
		B20 = True
		B21 = True
		Print "B17 = True;B20 = True;B21 = True"
	EndIf
    If bli67 = 2 Then
		B17 = False
		B20 = False
		B21 = True
	    Print "B17 = False;B20 = False;B21 = True"
	EndIf
	If bli67 = 3 Then
		B17 = False
		B20 = False
		B21 = False
	    Print "B17 = False;B20 = False;B21 = False"
	EndIf
	
   S11$ = S2$ + "_OKOK_a"; B1 = True
   Print "Visual switching succeeded"
    			
Fend


Function CPK_TEST

Tool 0
Weight 5 'Load setting 
Motor On 'Power on the motor 
'Fine 3, 3, 3, 3 '4 Axis positioning accuracy setting (pulse) 
FineDist 0.1 'Setting of target positioning accuracy (mm) 
Power High 'High power mode setting 
Speed 100 'Internal speed setting (%) 
SpeedS 2000 'Setting of linear speed (mm/sec) 

Accel 100, 100 'Setting of acceleration/deceleration ratio (%) 
AccelS 15000, 8000 'Setting of acceleration and deceleration value (mm/sec2) 
PTPBoost 75, 75, 75 'Micro-shift parameter setting 
'LimitTorque 20, 20, 20, 20 'Torque limit setting 

SpeedFactor 80
Integer gg
For gg = 1 To 60
	Move P70
	Wait 3
	Move P71
	Wait 3
Next

Fend


'================================================================================
' MOD@@ Guillermo Carrillo - v1.0.6.7.6 PRUEBA DE TOMA para la calibracion de vision
'   Lo usa la pantalla "Calibracion de vision" de la PC, con la maquina detenida.
'   019  CalTomaIr    : igual que TakeFeedDUT_1 (010) hasta ANTES de bajar a la pieza:
'                       va 5 mm arriba del punto de toma con el gripper abajo y SE QUEDA.
'                       No baja a la pieza, no cierra, no toma.
'   020  CalTomaJog   : corrimiento TOTAL X/Y/U desde el punto de toma, a la misma altura
'                       (+5 mm). Maximo 3 mm / 3 grados. Go al 10 %.
'   021  CalTomaSalir : sube el gripper.
'   Usa P47 (no estaba en robot1.pts) para guardar el punto de toma de la prueba.
'================================================================================
Function CalTomaIr(bli40 As Integer, bli41 As Integer, bli42 As Integer, blr40 As Real, blr41 As Real, blr42 As Real)
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On

  If bli41 < 1 Or bli41 > 42 Then
     S11$ = S2$ + "_E1627_a"; B1 = True
     Print "Prueba de toma: indice de bolsillo fuera de rango"
     S2$ = ""
     Exit Function
  EndIf
  If bli42 < 1 Or bli42 > 3 Then
     S11$ = S2$ + "_E1628_a"; B1 = True
     Print "Prueba de toma: indice de gripper fuera de rango"
     S2$ = ""
     Exit Function
  EndIf

  CalTomaGripper = bli42
  bli40 = bli40 + bli42
  LoadPoints "robot1.pts"
  P47 = P(bli40)
  CX(P47) = blr40
  CY(P47) = blr41
  CU(P47) = blr42
  SavePoints "robot1.pts"

  Tool bli42
  Hand P47, Lefty
  Call GripperClamp(bli42)
  Go P47 +Z(5) /L
  WaitPos
  Call GripperDown(bli42)
  Tool 0

  S2$ = ""
  S11$ = "019_OKOK_a"; B1 = True
  Print "Prueba de toma: gripper abajo, 5 mm sobre la pieza"
Fend

Function CalTomaJog(blr43 As Real, blr44 As Real, blr45 As Real)
  'MOD@@ v1.0.6.7.6: Go despacio (igual que el 014, que si funciona) en lugar de Move lineal.
  '                  Si el controlador da error al moverse contesta 020_<numero>_E1638_a.
  If CalTomaGripper < 1 Or CalTomaGripper > 3 Then
     S11$ = S2$ + "_E1628_a"; B1 = True
     Print "Prueba de toma: primero hay que mandar el 019"
     S2$ = ""
     Exit Function
  EndIf
  If blr43 < -3 Or blr43 > 3 Or blr44 < -3 Or blr44 > 3 Or blr45 < -3 Or blr45 > 3 Then
     S11$ = S2$ + "_E1637_a"; B1 = True
     Print "Prueba de toma: corrimiento mayor a 3 mm"
     S2$ = ""
     Exit Function
  EndIf

  OnErr GoTo CalTomaJogError
  Tool CalTomaGripper
  Speed 10; Accel 10, 10
  Go P47 +X(blr43) +Y(blr44) +Z(5) +U(blr45) /L
  WaitPos
  Speed 100; Accel 100, 100
  Tool 0

  S2$ = ""
  S11$ = "020_OKOK_a"; B1 = True
  Print "Prueba de toma: corrimiento hecho"
  Exit Function

CalTomaJogError:
  Speed 100; Accel 100, 100
  Tool 0
  Print "Prueba de toma: error del controlador al moverse"; Print Err
  S2$ = ""
  S11$ = "020_" + Str$(Err) + "_E1638_a"; B1 = True
  EResume CalTomaJogFin
CalTomaJogFin:
Fend

Function CalTomaSalir
  If CalTomaGripper >= 1 And CalTomaGripper <= 3 Then
     Call GripperUp(CalTomaGripper)
  EndIf
  Off G1Down; Off G2Down; Off G3Down; On G1Up; On G2Up; On G3Up
  Wait Sw(G1UpSignal) = On; Wait Sw(G2UpSignal) = On; Wait Sw(G3UpSignal) = On
  CalTomaGripper = 0
  Tool 0

  S2$ = ""
  S11$ = "021_OKOK_a"; B1 = True
  Print "Prueba de toma: gripper arriba"
Fend
