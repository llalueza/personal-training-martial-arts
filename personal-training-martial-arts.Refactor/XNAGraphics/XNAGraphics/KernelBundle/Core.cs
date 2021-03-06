using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using Registry = XNAGraphics.KernelBundle.BasicsBundle.BasicRegistry;
using XNAGraphics.KinectBundle.PostureBundle;
using XNAGraphics.KernelBundle.BasicsBundle;
using XNAGraphics.ComponentBundle.LayerBundle;
using XNAGraphics.ComponentBundle.MovementBundle;
using XNAGraphics.ComponentBundle.DrawableBundle;
using XNAGraphics.KinectBundle;
using XNAGraphics.KernelBundle.PlayerBundle;


namespace XNAGraphics.KernelBundle
{
    public class Core : XNAGraphics.KernelBundle.BasicsBundle.BasicCore
    {
        // ESTADOS DEL JUEGO Y PANTALLAS
        public enum screenState
        {
            INIT,
            MENU,
            SELECT_PLAYER,
            FOTO_PLAYER,
            MENU_PLAYER,
            PLAY,
            END
        }

        public enum playState
        {
            INIT,
            SELECT_POSTURE,
            DRAW_POSTURE,
            DETECT_POSTURE,
            HOLD_POSTURE,
            PAUSE,
            SCORE,
            FINAL_SCORE,
            END
        }

        public screenState currentScreenState, nextScreenState;
        public playState currentPlayState, nextPlayState;

        // TEMPORIZADORES (tiempos en segundos)
        private Stopwatch drawPostureTimeOut;
        private Stopwatch holdPostureTimeOut;
        private Stopwatch fotoTimeOut;
        private Stopwatch scoreTimeOut;
        private const int DRAW_POSTURE_TIME = 10;
        private const int FOTO_TIME = 5;
        private const int HOLD_POSTURE_TIME = 3;
        private const int SCORE_TIME = 10;

        // POSTURAS Y ESQUELTO
        private PostureInformation[] gamePostures;
        private int gamePosturesIndex;
        private GameScore gameScores;
        private int totalGameScoresRated;
        private double gameScoreRated;

        // Listado de jugadores (permitimos 4)
        private Player[] players = new Player[4];
        private int selected_player = 0;
        private Boolean ini_player = false;
        private Boolean textures_loaded = false;
        private Boolean player_selected_changed = false;
        private Boolean first_load = true;

        // NIVEL NORMAL para modificar usar metodo chDificultyLevel(int);
        private float averageTolerance = 0.058F;
        private float puntualTolerance = 0.07F;

        private double[] jointScore = new double[20];
        private double score;

        // Controlador del kinect
        public Kinect kinect;

        // TODO: ¿Esto aquí? Ya se verá... (Está aquí para que en los cambios de pantalla no se note el cambio brusco)
        Layer background, background_xbox;

        public Core(Game1 game)
            : base(game)
        {
            this.nextScreenState = screenState.INIT;
            this.nextPlayState = playState.INIT;
            this.gamePostures = null;
            this.gameScores = new GameScore();
            this.drawPostureTimeOut = Stopwatch.StartNew();
            this.holdPostureTimeOut = Stopwatch.StartNew();
            this.scoreTimeOut = Stopwatch.StartNew();
            this.fotoTimeOut = Stopwatch.StartNew();

            this.kinect = new Kinect();

            this.nextScreenState = screenState.INIT;
            this.nextPlayState = playState.INIT;

            loadPlayers();

            // TODO: CREAR SCROLLING_TEXT Y --SCROLLING_IMAGE-- Y SCROLLING_ANIMATION?? Y TILE Y SCROLLING_TILE??
        }

        public override Boolean onInitialize()
        {
            this.kinect.initialize();
            return true;
        }

        public override Boolean onLoadContent()
        {
            

            // Texture2D
            this.content.add("bg_new", "background_new");
            this.content.add("bg_xbox", "background_xbox");

            this.content.add("Age uke", "Age uke");
            this.content.add("Defensa Baja", "Defensa Baja");
            this.content.add("Grulla", "Grulla");
            this.content.add("Oi zuki", "Oi Zuki");
            this.content.add("Rei", "Rei");

            this.content.add("Age ukeS", "Age ukeS");
            this.content.add("Defensa BajaS", "Defensa BajaS");
            this.content.add("GrullaS", "GrullaS");
            this.content.add("Oi zukiS", "Oi ZukiS");
            this.content.add("ReiS", "ReiS");
            
            this.content.add("btn.menu", "btn.exit_to_menu");
            this.content.add("btn.play", "btn.play");
            this.content.add("btn.replay", "btn.replay_postures");
            this.content.add("btn.next", "btn.next");
            this.content.add("btn.exit", "btn.exit");
            this.content.add("btn.exit_to_menu", "btn.exit_to_menu");
            this.content.add("btn.continue", "btn.continue");
            this.content.add("btn.pause", "btn.pause");
            this.content.add("btn.borrar", "btn.borrar");
            this.content.add("skeleton.joint", "joint");

            // foto jugadores
            foreach (Player p in players)
            {
                this.content.add(p.getImageName(), p.getImageName());
            }

            // animacion inicial
            this.content.add("bruce","bruce");

            // Del logo
            this.content.add("logo.title", "title_personal_training");
            this.content.add("logo.edition", "title_martial_arts");

            // SpriteFont
            this.content.add("debug_text", "debug");
            this.content.add("normal_text", "arial");
            this.content.add("centered_text", "grobold");
            this.content.add("centered_text_small", "grobold_small");

            // Esto se hace siempre para que el ContentHandler lo cargue despues de haber añadido todas las texturas a manubrio
            this.content.load();

            update_players_foto();

            // Inicializamos nuestros señores fondos (que nos van a servir para todos y sin cambios bruscos al tenerlo como variable de clase)
            this.background = new Layer("Background", new ScrollingImage(this.content.get("bg_new"), this.game.graphics, 0, 0, Color.White, 30, 1f, 0), 1001);
            this.background_xbox = new Layer("BackgroundXBOX", new Image(this.content.get("bg_xbox"), 0, 0, Color.White * 0.85f), 1000);

            /**
             * Pantalla de inicio
             */
            LayerCollection home = new LayerCollection("Inicio",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("debug_text",
                    new InfoGraph(this.content.get("debug_text"), 10, 10)
                ),
                
                new Layer("panel de info",
                    new Panel(new Rectangle(421, 241, 820, 419), Color.Black * 0.9f, 5, Color.Black * 0.55f)
                ),
                new Layer("panel bruce",
                   // new AnimationPablo(this.content.get("bruce"), 421, 241, 820, 419, 9, 4, 34)
                     new AnimationPablo(this.content.get("bruce"), 600, 342, 9, 4, 34)
                ),
                new Layer("Btn play",
                    new Button(this.content.get("btn.play"), 195, 236)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit"), 195, 302)
                )
            ); r.add(home);

            /**
             * Pantalla de seleccion de jugador
             */
            LayerCollection select_player = new LayerCollection("select_player",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer(players[0].getImageName(),
                    new Button(players[0].foto, 140, 200, 0.52f)
                ),
                new Layer(players[1].getImageName(),
                    new Button(players[1].foto, 482, 200, 0.52f)
                ),
                new Layer(players[2].getImageName(),
                    new Button(players[2].foto, 140, 455, 0.52f)
                ),
                new Layer(players[3].getImageName(),
                    new Button(players[3].foto, 482, 455, 0.52f)
                )
            ); r.add(select_player);

            /**
             * Pantalla captura foto
             */
            LayerCollection foto_player = new LayerCollection("foto_player",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("Contenedor video",
                    new Panel(new Rectangle(200, 241, 646, 486), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("Texto feedback",
                    new BorderedText(this.content.get("centered_text_small"), "¡Sonríe ... ", this.game.GraphicsDevice.Viewport.Width / 2 - 300, 200, Color.Red, 2f, Color.Black)
                ),
                new Layer("Kinect RGB Video",
                    new KinectVideo(203, 244, this.kinect)
                )
            ); r.add(foto_player);

            /**
             * Pantalla menu de jugador
             */
            LayerCollection menu_player = new LayerCollection("menu_player",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("foto",
                    new Button(players[selected_player].foto, 150, 200, 0.75f)
                ),
                new Layer("Btn continue",
                    new Button(this.content.get("btn.continue"), 160, 570)
                ),
                new Layer("Btn borrar",
                    new Button(this.content.get("btn.borrar"), 410, 570)
                )
            ); r.add(menu_player);

            /**
             * Mostrar una postura a imitar
             */
            LayerCollection showPosture = new LayerCollection("Mostrar postura",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("Contenedor video",
                    new Panel(new Rectangle(200, 241, 646, 486), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("Contenedor info",
                    new Panel(new Rectangle(862, 241, 379, 419), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("Contenedor foto",
                    new Panel(new Rectangle(5, 5, 277, 210), Color.Beige * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("foto",
                    new Button(players[selected_player].foto, 10, 10, 0.42f)
                ),
                new Layer("texto chungo",
                    new Text(this.content.get("normal_text"), "postureando", 870, 290, Color.White)
                ),
                new Layer("Postura",
                    new Image(this.content.get("Rei"), 200, 241)
                //new Layer("Postura",
                //    new Skeleton(203, 244, this.kinect, new Posture())
                ),
                new Layer("Texto central",
                    new BorderedText(this.content.get("centered_text"), "Sin informacion de la postura", this.game.GraphicsDevice.Viewport.Width / 2, this.game.GraphicsDevice.Viewport.Height / 2, Color.Yellow, 3f, Color.Black)
                ),
                new Layer("Btn continue",
                    new Button(this.content.get("btn.continue"), 1047, 687)
                )
            ); r.add(showPosture);

            /**
             * Detectar postura
             */
            LayerCollection detectPosture = new LayerCollection("Detectar postura",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("Contenedor video",
                    new Panel(new Rectangle(200, 241, 646, 486), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("Contenedor info",
                    new Panel(new Rectangle(862, 241, 379, 419), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("Contenedor foto",
                    new Panel(new Rectangle(5, 5, 277, 210), Color.Beige * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("foto",
                    new Button(players[selected_player].foto, 10, 10, 0.42f)
                ),
                new Layer("Kinect RGB Video",
                    new KinectVideo(203, 244, this.kinect)
                ),
                new Layer("Profesor",
                    new Image(this.content.get("Rei"), 862, 241)
                //new Layer("Profesor",
                // TODO: Aquí va un skeleton
                //    new Skeleton(603, 174, this.kinect, new Posture())
                ),
                new Layer("Postura",
                    new ComparableSkeleton(203, 244, this.kinect, this.content.get("skeleton.joint"))
                ),
                new Layer("Texto central",
                    new BorderedText(this.content.get("centered_text"), "", this.game.GraphicsDevice.Viewport.Width / 2, this.game.GraphicsDevice.Viewport.Height / 2, Color.Yellow, 3f, Color.Black)
                ),
                new Layer("Texto feedback",
                    new BorderedText(this.content.get("centered_text_small"), "", this.game.GraphicsDevice.Viewport.Width / 2 -300, 200, Color.Red, 2f, Color.Black)
                ),
                new Layer("Btn pause",
                    new Button(this.content.get("btn.pause"), 1047, 687)
                )
            ); r.add(detectPosture);

            /**
             * Pantalla de pausa
             */
            LayerCollection pause = new LayerCollection("Pausa",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("debug_text",
                    new InfoGraph(this.content.get("debug_text"), 10, 10)
                ),
                new Layer("panel de info",
                    new Panel(new Rectangle(421, 241, 820, 419), Color.Black * 0.9f, 5, Color.Black * 0.55f)
                ),
                new Layer("Contenedor foto",
                    new Panel(new Rectangle(5, 5, 277, 210), Color.Beige * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("foto",
                    new Button(players[selected_player].foto, 10, 10, 0.42f)
                ),
                new Layer("Btn continue",
                    new Button(this.content.get("btn.continue"), 195, 236)
                ),
                new Layer("Btn replay",
                    new Button(this.content.get("btn.replay"), 195, 302)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit_to_menu"), 195, 368)
                )
            ); r.add(pause);

            /**
             * Pantalla de puntuación (posturíl)
             */
            LayerCollection postureScore = new LayerCollection("Puntuación de postura",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("Texto central",
                    new BorderedText(this.content.get("centered_text"), "Puntuación de la postura: ", this.game.GraphicsDevice.Viewport.Width / 2, this.game.GraphicsDevice.Viewport.Height / 2, Color.Green, 5f, Color.Black)
                ),
                new Layer("Contenedor foto",
                    new Panel(new Rectangle(5, 5, 277, 210), Color.Beige * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("foto",
                    new Button(players[selected_player].foto, 10, 10, 0.42f)
                ),
                new Layer("Btn next",
                    new Button(this.content.get("btn.next"), 1047, 687)
                ),
                new Layer("Btn replay",
                    new Button(this.content.get("btn.replay"), 826, 687)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit"), 605, 687)
                )
            ); r.add(postureScore);

            /**
             * Pantalla de puntuación (final)
             */
            LayerCollection final_score = new LayerCollection("Puntuación final",
                this.background, this.background_xbox,
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.title"), 500, 30)
                ),
                new Layer("Logo del juego",
                    new Image(this.content.get("logo.edition"), 730, 120)
                ),
                new Layer("Texto central",
                    new BorderedText(this.content.get("centered_text"), "Puntuación final: ", this.game.GraphicsDevice.Viewport.Width / 2, this.game.GraphicsDevice.Viewport.Height / 2, Color.Green, 5f, Color.Black)
                ),
                new Layer("Contenedor foto",
                    new Panel(new Rectangle(5, 5, 277, 210), Color.Beige * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("foto",
                    new Button(players[selected_player].foto, 10, 10, 0.42f)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit_to_menu"), 1047, 687)
                ),
                new Layer("Btn replay",
                    new Button(this.content.get("btn.replay"), 826, 687)
                )
            ); r.add(final_score);

            this.kinect.load(this.game);

            return true;
        }

        public override Boolean onUnloadContent()
        {
            this.kinect.unload();
            return true;
        }

        public override LayerCollection onUpdate(GameTime gameTime)
        {
            //this.r.get("Detectar postura").get("Texto central").drawable.addMovement(new Screw(1.05f, 0f, 30));
            //this.r.get("Detectar postura").get("Texto central").drawable.addMovement(new Screw(1f, 0f, 30));
            update_players_foto();

            this.currentScreenState = this.nextScreenState;
            this.currentPlayState = this.nextPlayState;

            switch (this.currentScreenState)
            {
                case screenState.INIT:
                    // algo en inicio?
                    first_load = false;
                    this.nextScreenState = screenState.MENU;
                    break;

                case screenState.MENU:
                    if ( ((Button)this.r.get("Inicio").get("btn play").drawable).justPushed() )
                    {
                        selected_player = -1;
                        this.nextScreenState = screenState.SELECT_PLAYER;
                    }
                    else if ( ((Button)this.r.get("Inicio").get("btn exit").drawable).justPushed() )
                    {
                        // termina el juego
                        this.game.Exit();
                       
                        //this.nextScreenState = screenState.END;
                    }
                    return this.r.get("Inicio");

                case screenState.SELECT_PLAYER:
                    if (selected_player != -1)
                    {
                        if (players[selected_player].active == false)
                        {
                            this.fotoTimeOut = Stopwatch.StartNew();
                            this.nextScreenState = screenState.FOTO_PLAYER;
                        }
                        else
                        {
                            player_selected_changed = true;
                            this.nextScreenState = screenState.MENU_PLAYER;
                        }
                    }
                    else
                        selected_player = calculate_selected_player();
                    return this.r.get("select_player");

                case screenState.FOTO_PLAYER:
                    if (isTimedOut(this.fotoTimeOut, FOTO_TIME))
                    {
                        if (players[selected_player].active == false)
                        {
                            kinect.locked = true;
                            players[selected_player].foto = kinect.kinectRGBVideo;
                            kinect.locked = false;
                            players[selected_player].active = true;
                            players[selected_player].save();
                            textures_loaded = true;
                            this.fotoTimeOut.Stop();
                            this.nextScreenState = screenState.MENU_PLAYER;
                        }
                        else
                            this.nextScreenState = screenState.MENU_PLAYER;
                    }
                    else
                    {
                            BorderedText te = (BorderedText)this.r.get("foto_player").get("Texto feedback").drawable;
                            te.text = "¡Sonríe ... " + (FOTO_TIME - this.fotoTimeOut.Elapsed.Seconds).ToString() + " segundos!";
                    }
                    return this.r.get("foto_player");

                case screenState.MENU_PLAYER:
                    if (((Button)this.r.get("menu_player").get("btn borrar").drawable).justPushed())
                    {
                        players[selected_player].active = false;
                        players[selected_player].foto = (Texture2D)this.content.get(players[selected_player].getImageName());
                        players[selected_player].lista_Resultados=  new List<double>();
                        players[selected_player].lista_Fechas = new List<DateTime>();
                        players[selected_player].save();
                        textures_loaded = true;
                        selected_player = -1;
                        this.nextScreenState = screenState.SELECT_PLAYER;
                    }
                    else if (((Button)this.r.get("menu_player").get("btn continue").drawable).justPushed())
                    {
                        this.nextScreenState = screenState.PLAY;
                        this.nextPlayState = playState.INIT;
                    }
                    return this.r.get("menu_player");

                case screenState.PLAY:
                    switch (this.currentPlayState)
                    {
                        case playState.INIT:
                            // algo en inicio?
                            this.gamePostures = null;
                            this.gameScores.gameScores.Clear();  // PABLO

                            this.nextPlayState = playState.SELECT_POSTURE;
                            break;

                        case playState.SELECT_POSTURE:
                            // actualiza la postura objetivo
                            if (updateCurrentGamePosture())
                            {
                                this.drawPostureTimeOut = Stopwatch.StartNew();
                                this.nextPlayState = playState.DRAW_POSTURE;
                            }
                            // si no quedan mas posturas va a la puntuacion final
                            else
                            {
                                gameScores.date = DateTime.Now;
                                
                                //players[selected_player].save(); -- La logica de guardado pasa al boton de salir en final score                                  
                                this.nextPlayState = playState.FINAL_SCORE;
                            }
                            break;

                        case playState.DRAW_POSTURE:
                            // Esta fase es para presentarle al usuario la postura objetivo
                            // TIMEOUT de 10 segundos o pulsar CONTINUE
                        
                            (this.r.get("Mostrar postura").get("texto chungo").drawable) =  new Text(this.content.get("normal_text"), this.gamePostures[this.gamePosturesIndex].description, 870, 290, Color.White);// this.gamePostures[this.gamePosturesIndex].description;
                            this.r.get("Mostrar postura").get("Postura").drawable = new Image(this.content.get(this.gamePostures[this.gamePosturesIndex].name), 200, 241);
                            this.r.get("Detectar postura").get("Profesor").drawable = new Image(this.content.get(this.gamePostures[this.gamePosturesIndex].name+"S"), 862, 241);
                            //((Skeleton)this.r.get("Mostrar postura").get("Postura").drawable).posture = this.gamePostures[this.gamePosturesIndex];
                            //((Skeleton)this.r.get("Detectar postura").get("Profesor").drawable).posture = this.gamePostures[this.gamePosturesIndex];
                            ((ComparableSkeleton)this.r.get("Detectar postura").get("Postura").drawable).postureToCompare = this.gamePostures[this.gamePosturesIndex];
                            BorderedText t = (BorderedText)this.r.get("Mostrar postura").get("Texto central").drawable;
                            t.text = this.gamePostures[this.gamePosturesIndex].name;

                            if ( ((Button)this.r.get("Mostrar postura").get("btn continue").drawable).justPushed() ||
                                isTimedOut(this.drawPostureTimeOut, DRAW_POSTURE_TIME))
                            {
                                this.drawPostureTimeOut.Reset();
                                this.nextPlayState = playState.DETECT_POSTURE;
                            }
                            return this.r.get("Mostrar postura");

                        case playState.DETECT_POSTURE:
                            
                            //updateButtonsState(this.gameButtons);
                            if ( ((Button)this.r.get("Detectar postura").get("btn pause").drawable).justPushed() )
                                this.nextPlayState = playState.PAUSE;
                            else
                            {
                                BorderedText ti = (BorderedText)this.r.get("Detectar postura").get("Texto feedback").drawable;
                                BorderedText te = (BorderedText)this.r.get("Detectar postura").get("Texto central").drawable;
                                te.text = "";
                                if (this.kinect.skeleton != null && !this.kinect.skeletonOutOfRange)
                                {
                                    ((ComparableSkeleton)this.r.get("Detectar postura").get("Postura").drawable).hiden = false;
                                    Posture p = new Posture(this.kinect.skeleton);
                                    score = p.compareTo(gamePostures[gamePosturesIndex], ref jointScore, averageTolerance, puntualTolerance);
                                    ti.text = getPostureTextFeedback(score);

                                    if (score < 1.0)
                                    {
                                        this.holdPostureTimeOut = Stopwatch.StartNew();
                                        this.nextPlayState = playState.HOLD_POSTURE;
                                        gameScoreRated = score;
                                        this.totalGameScoresRated = 1;
                                    }
                                }
                                else
                                {
                                    ((ComparableSkeleton)this.r.get("Detectar postura").get("Postura").drawable).hiden = true;
                                    ti.text = "¡Situate en la pantalla!";
                                }
                            }
                            return this.r.get("Detectar postura");

                        case playState.HOLD_POSTURE:
                            //updateButtonsState(this.gameButtons);
                            if ( ((Button)this.r.get("Detectar postura").get("btn pause").drawable).justPushed() )
                                this.nextPlayState = playState.PAUSE;
                            else
                            {
                                BorderedText ti = (BorderedText)this.r.get("Detectar postura").get("Texto feedback").drawable;
                                if (this.kinect.skeleton != null && !this.kinect.skeletonOutOfRange)
                                {
                                    BorderedText te = (BorderedText)this.r.get("Detectar postura").get("Texto central").drawable;
                                    te.text = "¡Quédate quieto durante " + (HOLD_POSTURE_TIME - this.holdPostureTimeOut.Elapsed.Seconds).ToString() + " segundos!";
                                    te.addMovement(new Screw(1.05f, 0f, 25));
                                    te.addMovement(new Screw(1f, 0f, 25));
                                    //this.r.get("Detectar postura").get("Texto central").drawable = t;

                                    Posture p = new Posture(this.kinect.skeleton);
                                    score = p.compareTo(gamePostures[gamePosturesIndex], ref jointScore, averageTolerance, puntualTolerance);
                                    ti.text = getPostureTextFeedback(score);
                                    if (score < 1.0)
                                    {
                                        this.totalGameScoresRated++;
                                        gameScoreRated += score;
                                        // La postura hay que mantenerla 2 segundos (HOLD_POSTURE_TIME)
                                        if (isTimedOut(this.holdPostureTimeOut, HOLD_POSTURE_TIME))
                                        {
                                            gameScores.gameScores.Add(gamePostures[gamePosturesIndex], gameScoreRated / totalGameScoresRated);
                                            this.holdPostureTimeOut.Reset();
                                            this.scoreTimeOut = Stopwatch.StartNew();
                                            this.nextPlayState = playState.SCORE;
                                        }
                                    }
                                    else
                                    {
                                        this.totalGameScoresRated = 0;
                                        this.gameScoreRated = 0;
                                        this.holdPostureTimeOut.Reset();
                                        this.nextPlayState = playState.DETECT_POSTURE;
                                    }
                                }
                            }
                            return this.r.get("Detectar postura");

                        case playState.PAUSE:
                            if ( ((Button)this.r.get("Pausa").get("btn continue").drawable).justPushed() )
                                this.nextPlayState = playState.DETECT_POSTURE;
                            else if ( ((Button)this.r.get("Pausa").get("btn replay").drawable).justPushed() )
                                this.nextPlayState = playState.INIT;
                            else if ( ((Button)this.r.get("Pausa").get("btn exit").drawable).justPushed() )
                                this.nextPlayState = playState.END;
                            
                            return this.r.get("Pausa");

                        case playState.SCORE:
                            //updateButtonsState(this.scoreButtons);
                            // TIMEOUT de 10 segundos a la siguiente postura o se pulsa alguna opcion
                            if ( ((Button)this.r.get("Puntuación de postura").get("btn next").drawable).justPushed() ||
                                isTimedOut(this.scoreTimeOut, SCORE_TIME))
                            {
                                this.scoreTimeOut.Reset();
                                this.nextPlayState = playState.SELECT_POSTURE;
                            }
                            else if ( ((Button)this.r.get("Puntuación de postura").get("btn exit").drawable).justPushed() )
                            {
                                this.scoreTimeOut.Reset();
                                this.nextPlayState = playState.END;
                            }
                            else if ( ((Button)this.r.get("Puntuación de postura").get("btn replay").drawable).justPushed() )
                            {
                                this.scoreTimeOut.Reset();
                                this.nextPlayState = playState.INIT;
                            }
                            BorderedText scoreText = (BorderedText)this.r.get("Puntuación de postura").get("Texto Central").drawable;
                            scoreText.text = "Puntuación de la postura: " + calculateScorePercent(this.gameScores.gameScores[gamePostures[gamePosturesIndex]]) + "%";
                            return this.r.get("Puntuación de postura");

                        case playState.FINAL_SCORE:

                            BorderedText finalScoreText = (BorderedText)this.r.get("Puntuación final").get("Texto Central").drawable;
                            double total = 0;
                            foreach (double s in gameScores.gameScores.Values)
                            {
                                total += s;
                            }
                            finalScoreText.text = "Puntuación final: " + calculateScorePercent(total / this.gameScores.gameScores.Count) + "%  \n "+ players[this.selected_player].getHistoric();

                            

                            if (((Button)this.r.get("Puntuación final").get("btn exit").drawable).justPushed())
                            {
                                this.nextPlayState = playState.END;
                                players[selected_player].lista_Resultados.Add(calculateScorePercent(total / this.gameScores.gameScores.Count));
                                
                                players[selected_player].lista_Fechas.Add(DateTime.Now);
                                players[selected_player].save();
                            }
                            else if (((Button)this.r.get("Puntuación final").get("btn replay").drawable).justPushed())
                                this.nextPlayState = playState.INIT;



                            return this.r.get("Puntuación final");

                        

                        default:
                        case playState.END:
                            this.nextScreenState = screenState.MENU;
                            break;
                    }
                    break;

                default:
                case screenState.END:
                    return this.r.get("Detectar postura");
            }

            return this.r.get("Detectar postura");
        }

        public override LayerCollection onDraw(GameTime gameTime)
        {
            if (this.currentScreenState == screenState.MENU)
            {
                return this.r.get("Inicio");
            }
            else if (this.currentScreenState == screenState.SELECT_PLAYER)
            {
                return this.r.get("select_player");
            }
            else if (this.currentScreenState == screenState.FOTO_PLAYER)
            {
                return this.r.get("foto_player");
            }
            else if (this.currentScreenState == screenState.MENU_PLAYER)
            {
                return this.r.get("menu_player");
            }
            else if (this.currentScreenState == screenState.PLAY)
            {
                if (this.currentPlayState == playState.DRAW_POSTURE)
                {
                    return this.r.get("Mostrar postura");
                }
                else if (this.currentPlayState == playState.DETECT_POSTURE)
                {
                    return this.r.get("Detectar postura");
                }
                else if (this.currentPlayState == playState.HOLD_POSTURE)
                {
                    // FIX: HACER QUE SE MUESTRE EL MENSAJITO EN EL CENTRO
                    return this.r.get("Detectar postura"); 
                }
                else if (this.currentPlayState == playState.PAUSE)
                {
                    return this.r.get("Pausa");
                }
                else if (this.currentPlayState == playState.SCORE)
                {
                    return this.r.get("Puntuación de postura");
                }
                else if (this.currentPlayState == playState.FINAL_SCORE)
                {
                    return this.r.get("Puntuación final");
                }
                else
                {
                    // Loading...
                } // End if playState
            } // End if screenState

            return this.r.get("Inicio");
        }
        
        /// <summary>
        /// Comprueba si se ha cumplido un timeout.
        /// </summary>
        /// <param name="startTime">DateTime de inicio</param>
        /// <param name="secondsToTimeOut">Segundos para timeout</param>
        /// <returns>Si los segundos se han pasado o no</returns>
        public static Boolean isTimedOut(Stopwatch sw, int secondsToTimeOut)
        {
            TimeSpan maxDuration = TimeSpan.FromSeconds(secondsToTimeOut);

            if (sw.Elapsed < maxDuration)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Actualiza la postura actual. Si no hay posturas, las carga.
        /// False si no hay mas posturas que actualizar
        /// </summary>
        private Boolean updateCurrentGamePosture()
        {
            // Se piden las posturas a PostureLibrary, se randomiza y se selecciona la primera
            // sino, se avanza a la siguiente...
            if (this.gamePostures == null)
            {
                /*
                this.gamePostures = PostureLibrary.getPostureList();
                //Copiar todos los XML a Sqlite
               
                */
                this.gamePostures = PostureLibraryFromDB.getPostureList(); // MODIFICACION BESTIA DE PABLO PARA PROBAR
                this.shufflePostures(gamePostures);
                this.gamePosturesIndex = 0;
                return true;
            }
            // si no hay mas posturas que sacar se termina el juego
            else if (this.gamePosturesIndex ==  gamePostures.Length - 1)
            {
                this.gamePostures = null;
                return false;
            }
            // sino se avanza la postura
            else
            {
                this.gamePosturesIndex++;
                return true;
            }
        }

        /// ?? Esto iría mejor en Posture.Posture (static) ??
        /// <summary>
        /// Mezcla un array de <code>Posture</code>.
        /// </summary>
        /// <param name="postures">Posturas a mezclar</param>
        private void shufflePostures(PostureInformation[] postures)
        {
            for (int t = 0; t < postures.Length; t++)
            {
                PostureInformation tmp = postures[t];
                Random rr = new Random();
                int r = rr.Next(t, postures.Length);
                postures[t] = postures[r];
                postures[r] = tmp;
            }
        }

        private void shufflePostures2(PostureInformation[] postures)
        {
            postures[0] = postures[4];
        }

        public static String getPostureTextFeedback(double score)
        {
            if (score >= 3)
                return "Fijate bien en el ejemplo";
            else if (score >= 2)
                return "¡Te vas acercando!";
            else if (score >= 1)
                return "¡Casi la tienes!";
            else
                return "¡Eres un maquina!";
        }

        private static int calculateScorePercent(double score)
        {
            double res = 1 - score;
            res = (res) * 100;
            int total = (int)res;
            return total + 50;
        }

        private void loadPlayers()
        {
            players[0] = Player.loadPlayer(0, null);
            players[1] = Player.loadPlayer(1, null);
            players[2] = Player.loadPlayer(2, null);
            players[3] = Player.loadPlayer(3, null);
        }

        private int calculate_selected_player()
        {
            foreach(Player p in players)
            {
                if (((Button)this.r.get("select_player").get(p.getImageName()).drawable).justPushed())
                {
                    return p.id;
                }
            }
            return -1;
        }

        private void update_players_foto()
        {
            if (!ini_player)
            {
                foreach (Player p in players)
                {
                        p.foto = new Texture2D(game.GraphicsDevice, 640, 480);
                        Color[] sex = new Color[640 * 480];
                        ((Texture2D)this.content.get(players[p.id].getImageName())).GetData(sex);
                        p.foto.SetData(sex);
                }
                
                ini_player = true;
            }
            else
            {
                if (first_load || textures_loaded || player_selected_changed)
                    load_players_foto();

                //int i = 0;
                foreach (Player p in players)
                {
                    if (p.active == false && (first_load || textures_loaded || player_selected_changed) ){
                        p.foto = new Texture2D(game.GraphicsDevice, 640, 480);
                        Color[] sex = new Color[640 * 480];
                        ((Texture2D)this.content.get(players[p.id].getImageName())).GetData(sex);
                        p.foto.SetData(sex);
                        
                    }
                    if (first_load || textures_loaded)
                    {
                        Button f = (Button)this.r.get("select_player").get(p.getImageName()).drawable;
                        f.sprite = p.foto;
                      //  load_player_foto(i);
                    }
                   // i++;
                }
            }
            if (player_selected_changed || (textures_loaded && selected_player != -1))
            {
                ((Button)this.r.get("menu_player").get("foto").drawable).sprite = players[selected_player].foto;
                ((Button)this.r.get("Mostrar postura").get("foto").drawable).sprite = players[selected_player].foto;
                ((Button)this.r.get("Detectar postura").get("foto").drawable).sprite = players[selected_player].foto;
                ((Button)this.r.get("Pausa").get("foto").drawable).sprite = players[selected_player].foto;
                ((Button)this.r.get("Puntuación de postura").get("foto").drawable).sprite = players[selected_player].foto;
                ((Button)this.r.get("Puntuación final").get("foto").drawable).sprite = players[selected_player].foto;
                player_selected_changed = false;
            }
            textures_loaded = false;
        }

        private void load_players_foto()
        {
            players[0].load(new Texture2D(game.GraphicsDevice, 640, 480));
            players[1].load(new Texture2D(game.GraphicsDevice, 640, 480));
            players[2].load(new Texture2D(game.GraphicsDevice, 640, 480));
            players[3].load(new Texture2D(game.GraphicsDevice, 640, 480));
        }

        private void load_player_foto(int i)
        {
            players[i].load(new Texture2D(game.GraphicsDevice, 640, 480));
            
        }
    }
}
