﻿using System;
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

namespace XNAGraphics.KernelBundle
{
    class Core : XNAGraphics.KernelBundle.BasicsBundle.BasicCore
    {
        // ESTADOS DEL JUEGO Y PANTALLAS
        private enum screenState
        {
            INIT,
            MENU,
            PLAY,
            END
        }

        private enum playState
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

        private screenState currentScreenState, nextScreenState;
        private playState currentPlayState, nextPlayState;

        // TEMPORIZADORES (tiempos en segundos)
        private Stopwatch drawPostureTimeOut;
        private Stopwatch holdPostureTimeOut;
        private Stopwatch scoreTimeOut;
        private const int DRAW_POSTURE_TIME = 10;
        private const int HOLD_POSTURE_TIME = 3;
        private const int SCORE_TIME = 10;

        // POSTURAS Y ESQUELTO
        private PostureInformation[] gamePostures;
        private int gamePosturesIndex;
        private Dictionary<PostureInformation, double> gameScores;

        // NIVEL NORMAL para modificar usar metodo chDificultyLevel(int);
        private float averageTolerance = 0.058F;
        private float puntualTolerance = 0.07F;

        private double[] jointScore = new double[20];
        private double score;

        // Controlador del kinect
        Kinect kinect;

        // TODO: ¿Esto aquí? Ya se verá... (Está aquí para que en los cambios de pantalla no se note el cambio brusco)
        Layer background;

        public Core(Game1 game)
            : base(game)
        {
            this.nextScreenState = screenState.INIT;
            this.nextPlayState = playState.INIT;
            this.gamePostures = null;
            this.gameScores = new Dictionary<PostureInformation, double>();
            this.drawPostureTimeOut = Stopwatch.StartNew();
            this.holdPostureTimeOut = Stopwatch.StartNew();
            this.scoreTimeOut = Stopwatch.StartNew();





            this.kinect = new Kinect();

            this.nextScreenState = screenState.INIT;
            this.nextPlayState = playState.INIT;

            this.drawPostureTimeOut = Stopwatch.StartNew();
            this.holdPostureTimeOut = Stopwatch.StartNew();
            this.scoreTimeOut = Stopwatch.StartNew();

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
            this.content.add("bg", "background");
            this.content.add("bgnew", "new_bg");
            this.content.add("btn.menu", "menu");
            this.content.add("btn.play", "play");
            this.content.add("btn.replay", "play");
            this.content.add("btn.next", "next");
            this.content.add("btn.exit", "exit");
            this.content.add("btn.continue", "continue");
            this.content.add("btn.pause", "pause");
            this.content.add("skeleton.joint", "joint");

            // Del video
            this.content.add("video.header", "video_header");
            this.content.add("video.footer", "video_footer");
            this.content.add("video.waiting", "waiting");

            // SpriteFont
            this.content.add("debug", "debug");
            this.content.add("arial", "arial");
            this.content.add("grobold", "grobold");

            // Esto se hace siempre para que el ContentHandler lo cargue despues de haber añadido todas las texturas a manubrio
            this.content.load();

            // Inicializamos nuestro señor fondo (que nos va a servir para todos y sin cambios bruscos al tenerlo como variable de clase)
            this.background = new Layer("Background", new ScrollingImage(this.content.get("bgnew"), this.game.graphics, 0, 0, Color.White, 30, 1f, 0), 1000);

            /**
             * Pantalla de inicio
             */
            LayerCollection home = new LayerCollection("Inicio",
                this.background,
                new Layer("Logo del juego",
                    //new Image(this.content.get("logo"), 30, 30)
                    new Text(this.content.get("arial"), "Personal Training: Martial Arts", 100, 100)
                ),
                new Layer("debug",
                //new Image(this.content.get("logo"), 30, 30)
                    new InfoGraph(this.content.get("debug"), 10, 10)
                ),
                new Layer("Btn play",
                    new Button(this.content.get("btn.play"), 30, this.game.GraphicsDevice.Viewport.Height - 160)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit"), 300, this.game.GraphicsDevice.Viewport.Height - 160)
                )
            ); r.add(home);

            /**
             * Mostrar una postura a imitar
             */
            LayerCollection showPosture = new LayerCollection("Mostrar postura",
                this.background,
                new Layer("Contenedor video",
                    new Panel(new Rectangle(28, 28, 644, 543), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("video_header",
                    new Image(this.content.get("video.header"), 30, 30)
                ),
                new Layer("Postura",
                    // TODO: Aquí va un skeleton
                    new Skeleton(30, 78, this.kinect, new Posture())
                    //new Image(this.content.get("video.waiting"), 30, 78)
                ),
                new Layer("video_footer",
                    new Image(this.content.get("video.footer"), 30, 558)
                ),
                new Layer("Texto central",
                    new BorderedText(this.content.get("grobold"), "Sin informacion de la postura", this.game.GraphicsDevice.Viewport.Width / 2, this.game.GraphicsDevice.Viewport.Height / 2, Color.Yellow, 3f, Color.Black)
                ),
                new Layer("Btn continue",
                    new Button(this.content.get("btn.continue"), 300, this.game.GraphicsDevice.Viewport.Height - 160)
                )
            ); r.add(showPosture);

            /**
             * Detectar postura
             */
            LayerCollection detectPosture = new LayerCollection("Detectar postura",
                this.background,
                new Layer("Contenedor video",
                    new Panel(new Rectangle(28, 28, 644, 543), Color.Black * 0.95f, 2, Color.Gray * 0.9f)
                ),
                new Layer("video_header",
                    new Image(this.content.get("video.header"), 30, 30)
                ),
                new Layer("Kinect RGB Video",
                    new KinectVideo(30, 78, this.kinect)
                ),
                new Layer("Profesor",
                // TODO: Aquí va un skeleton
                    new Skeleton(30, 78, this.kinect, new Posture())
                ),
                new Layer("Postura",
                    new ComparableSkeleton(30, 78, this.kinect, this.content.get("skeleton.joint"))
                ),
                new Layer("video_footer",
                    new Image(this.content.get("video.footer"), 30, 558)
                ),
                new Layer("Texto central",
                    new BorderedText(this.content.get("grobold"), "", this.game.GraphicsDevice.Viewport.Width / 2, this.game.GraphicsDevice.Viewport.Height / 2, Color.ForestGreen, 3f, Color.Black)
                ),
                new Layer("Btn pause",
                    new Button(this.content.get("btn.pause"), 300, this.game.GraphicsDevice.Viewport.Height - 160)
                )
            ); r.add(detectPosture);

            /**
             * Pantalla de pausa
             */
            LayerCollection pause = new LayerCollection("Pausa",
                this.background,
                new Layer("Btn continue",
                    new Button(this.content.get("btn.continue"), 0, this.game.GraphicsDevice.Viewport.Height - 160)
                ),
                new Layer("Btn replay",
                    new Button(this.content.get("btn.replay"), 300, this.game.GraphicsDevice.Viewport.Height - 160)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit"), 500, this.game.GraphicsDevice.Viewport.Height - 160)
                )
            ); r.add(pause);

            /**
             * Pantalla de puntuación (posturíl)
             */
            LayerCollection postureScore = new LayerCollection("Puntuación de postura",
                this.background,
                new Layer("Texto central",
                    new BorderedText(this.content.get("grobold"), "Puntuación de la postura: 5.782", 500, this.game.GraphicsDevice.Viewport.Height/2, Color.DarkRed, 3f, Color.Black)
                ),
                new Layer("Btn next",
                    new Button(this.content.get("btn.next"), 0, this.game.GraphicsDevice.Viewport.Height - 160)
                ),
                new Layer("Btn replay",
                    new Button(this.content.get("btn.replay"), 300, this.game.GraphicsDevice.Viewport.Height - 160)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit"), 500, this.game.GraphicsDevice.Viewport.Height - 160)
                )
            ); r.add(postureScore);

            /**
             * Pantalla de puntuación (final)
             */
            LayerCollection final_score = new LayerCollection("Puntuación final",
                this.background,
                new Layer("Texto central",
                    new BorderedText(this.content.get("grobold"), "Puntuación final: 3.983", 500, this.game.GraphicsDevice.Viewport.Height/2, Color.DarkRed, 3f, Color.Black)
                ),
                new Layer("Btn exit",
                    new Button(this.content.get("btn.exit"), 0, this.game.GraphicsDevice.Viewport.Height - 60)
                ),
                new Layer("Btn replay",
                    new Button(this.content.get("btn.replay"), 300, this.game.GraphicsDevice.Viewport.Height - 60)
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


            this.currentScreenState = this.nextScreenState;
            this.currentPlayState = this.nextPlayState;

            switch (this.currentScreenState)
            {
                case screenState.INIT:
                    // algo en inicio?
                    this.nextScreenState = screenState.MENU;
                    break;

                case screenState.MENU:
                    if ( ((Button)this.r.get("Inicio").get("btn play").drawable).justPushed() )
                    {
                        this.nextScreenState = screenState.PLAY;
                        this.nextPlayState = playState.INIT;
                    }
                    else if ( ((Button)this.r.get("Inicio").get("btn exit").drawable).justPushed() )
                    {
                        // termina el juego
                        this.game.Exit();
                        //this.nextScreenState = screenState.END;
                    }

                    return this.r.get("Inicio");

                case screenState.PLAY:
                    switch (this.currentPlayState)
                    {
                        case playState.INIT:
                            // algo en inicio?
                            this.gamePostures = null;
                            this.gameScores.Clear();

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
                                this.nextPlayState = playState.FINAL_SCORE;
                            }
                            break;

                        case playState.DRAW_POSTURE:
                            // Esta fase es para presentarle al usuario la postura objetivo
                            // TIMEOUT de 10 segundos o pulsar CONTINUE
                            ((Skeleton)this.r.get("Mostrar postura").get("Postura").drawable).posture = this.gamePostures[this.gamePosturesIndex];
                            ((Skeleton)this.r.get("Detectar postura").get("Profesor").drawable).posture = this.gamePostures[this.gamePosturesIndex];
                            ((ComparableSkeleton)this.r.get("Detectar postura").get("Postura").drawable).postureToCompare = this.gamePostures[this.gamePosturesIndex];

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
                                if (this.kinect.skeleton != null)
                                {
                                    BorderedText t = (BorderedText)this.r.get("Detectar postura").get("Texto central").drawable;
                                    t.text = "";

                                    Posture p = new Posture(this.kinect.skeleton);
                                    score = p.compareTo(gamePostures[gamePosturesIndex], ref jointScore, averageTolerance, puntualTolerance);
                                    if (score < 1.0)
                                    {
                                        this.holdPostureTimeOut = Stopwatch.StartNew();
                                        this.nextPlayState = playState.HOLD_POSTURE;
                                    }
                                }
                            }
                            return this.r.get("Detectar postura");

                        case playState.HOLD_POSTURE:
                            //updateButtonsState(this.gameButtons);
                            if ( ((Button)this.r.get("Detectar postura").get("btn pause").drawable).justPushed() )
                                this.nextPlayState = playState.PAUSE;
                            else
                            {
                                if (this.kinect.skeleton != null)
                                {
                                    BorderedText t = (BorderedText)this.r.get("Detectar postura").get("Texto central").drawable;
                                    t.text = "Quedate quieto durante " + (HOLD_POSTURE_TIME - this.holdPostureTimeOut.Elapsed.Seconds + 1).ToString() + " segundos!!";
                                    //this.r.get("Detectar postura").get("Texto central").drawable = t;

                                    Posture p = new Posture(this.kinect.skeleton);
                                    score = p.compareTo(gamePostures[gamePosturesIndex], ref jointScore, averageTolerance, puntualTolerance);
                                    if (score < 1.0)
                                    {
                                        // La postura hay que mantenerla 2 segundos (HOLD_POSTURE_TIME)
                                        if (isTimedOut(this.holdPostureTimeOut, HOLD_POSTURE_TIME))
                                        {
                                            gameScores.Add(gamePostures[gamePosturesIndex], score);
                                            this.holdPostureTimeOut.Reset();
                                            this.scoreTimeOut = Stopwatch.StartNew();
                                            this.nextPlayState = playState.SCORE;
                                        }
                                    }
                                    else
                                    {
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
                            break;

                        case playState.FINAL_SCORE:
                            //updateButtonsState(this.scoreButtons);
                            if ( ((Button)this.r.get("Puntuación final").get("btn exit").drawable).justPushed() )
                                this.nextPlayState = playState.END;
                            else if ( ((Button)this.r.get("Puntuación final").get("btn replay").drawable).justPushed() )
                                this.nextPlayState = playState.INIT;
                            break;

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
                this.gamePostures = PostureLibrary.getPostureList();
                this.shufflePostures(gamePostures);
                this.gamePosturesIndex = 0;
                return true;
            }
            // si no hay mas posturas que sacar se termina el juego
            else if (this.gamePosturesIndex == gamePostures.Length - 1)
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
    }
}
