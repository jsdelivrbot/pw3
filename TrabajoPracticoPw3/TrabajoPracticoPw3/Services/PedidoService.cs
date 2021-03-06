﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TrabajoPracticoPw3.Api.Model;
using TrabajoPracticoPw3.Helper;
using TrabajoPracticoPw3.Models;
using TrabajoPracticoPw3.Models.Email;

namespace TrabajoPracticoPw3.Services
{
    public class PedidoService
    {
        TPEntities ctx = new TPEntities();
        EmailService es = new EmailService();

        public Usuario BuscarUsuarioById(int idUsuario)
        {
            Usuario usuarioEncontrado = ctx.Usuario.SingleOrDefault(x => x.IdUsuario == idUsuario);
            return usuarioEncontrado;
        }

        //------------------------------Negocio------------------------------

        public int IniciarService(FormCollection form, Usuario usuarioLogueado)
        {
            Usuario uLogueado = ctx.Usuario.Find(usuarioLogueado.IdUsuario);
            Pedido nuevoPedido = new Pedido
            {
                NombreNegocio = form["NombreNegocio"],
                Descripcion = form["Descripcion"],
                PrecioUnidad = int.Parse(form["PrecioUnidad"]),
                PrecioDocena = int.Parse(form["PrecioDocena"]),
                FechaCreacion = DateTime.Now,
                Usuario = uLogueado,
                //IdUsuarioResponsable = usuarioLogueado.IdUsuario,
                //EstadoPedido = ctx.EstadoPedido.SingleOrDefault(x => x.Nombre == "Abierto")
                EstadoPedido = ctx.EstadoPedido.Where(x => x.Nombre == "Abierto").FirstOrDefault(),
            };


            int[] gustosDisponibles = Array.ConvertAll(form.GetValues("gustosDisponibles"), int.Parse);
            int[] invitados = Array.ConvertAll(form.GetValues("invitados"), int.Parse);

            foreach (int gustoId in gustosDisponibles)
            {
                GustoEmpanada gustoEncontrado = ctx.GustoEmpanada.SingleOrDefault(x => x.IdGustoEmpanada == gustoId);
                nuevoPedido.GustoEmpanada.Add(gustoEncontrado);

            }

            List<int> Listainvitados = invitados.OfType<int>().ToList();
            Listainvitados.Add(usuarioLogueado.IdUsuario);

            foreach (int invitadoId in Listainvitados)
            {
                Usuario usuarioEncontrado = ctx.Usuario.SingleOrDefault(x => x.IdUsuario == invitadoId);
                InvitacionPedido invitacionPedido = new InvitacionPedido
                {
                    Pedido = nuevoPedido,
                    Usuario = usuarioEncontrado,
                    Token = new Guid(new Md5Hash().GetMD5((usuarioEncontrado.Email + nuevoPedido.FechaCreacion))),
                    Completado = false,
                };
                ctx.InvitacionPedido.Add(invitacionPedido);
                es.EnviarEmailInvitados(invitacionPedido);
            }

            ctx.Pedido.Add(nuevoPedido);

            try { ctx.SaveChanges(); }
            catch (DbEntityValidationException dbEx)
            {
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        Trace.TraceInformation("Property: {0} Error: {1}",
                            validationError.PropertyName,
                            validationError.ErrorMessage);
                    }
                }
            }

            return nuevoPedido.IdPedido;
        }

        public PedidoInfoJson ObtenerPedidoByJson(int id)
        {
            Pedido pedido = ObtenerPedidoById(id);
            PedidoInfoJson pedidoJson = new PedidoInfoJson
            {
                NombreNegocio = pedido.NombreNegocio,
                CantidadInvitados = pedido.InvitacionPedido.Where(i => i.Completado == true).Count()
            };

            return pedidoJson;
        }

        public int EliminarService(int id)
        {
            //TODO: Mostrar por pantalla el pedido y la cantidad de invitados
            Pedido pedido = ObtenerPedidoById(id);
            List<int> gustoEmpanadasIds = new List<int>();
            List<int> invitacionPedidoIds = new List<int>();
            List<int> invitacionPedidoGustoEmpanadaUsuarioIds = new List<int>();

            //Agregacion
            foreach (var gusto in pedido.GustoEmpanada)
            {
                gustoEmpanadasIds.Add(gusto.IdGustoEmpanada);
            }

            foreach (var invitacion in pedido.InvitacionPedido)
            {
                invitacionPedidoIds.Add(invitacion.IdInvitacionPedido);
            }
            foreach (var invitacionPedidoGustoEmpanadaUsuario in pedido.InvitacionPedidoGustoEmpanadaUsuario)
            {
                invitacionPedidoGustoEmpanadaUsuarioIds.Add(invitacionPedidoGustoEmpanadaUsuario.IdInvitacionPedidoGustoEmpanadaUsuario);
            }

            //Eliminacion 
            foreach (var idGustos in gustoEmpanadasIds)
            {
                var gustoEliminar = ctx.GustoEmpanada.FirstOrDefault(g => g.IdGustoEmpanada == idGustos);
                pedido.GustoEmpanada.Remove(gustoEliminar);
            }

            foreach (var idInvitacion in invitacionPedidoIds)
            {
                var invitacionEliminar = ctx.InvitacionPedido.FirstOrDefault(i => i.IdInvitacionPedido == idInvitacion);
                ctx.InvitacionPedido.Remove(invitacionEliminar);
            }

            foreach (var idInvitacionPedidoGustoEmpanadaUsuario in invitacionPedidoGustoEmpanadaUsuarioIds)
            {
                var invitacionPedidoGustoEmpanadaUsuarioEliminar = ctx.InvitacionPedidoGustoEmpanadaUsuario.FirstOrDefault(i => i.IdInvitacionPedidoGustoEmpanadaUsuario == idInvitacionPedidoGustoEmpanadaUsuario);
                ctx.InvitacionPedidoGustoEmpanadaUsuario.Remove(invitacionPedidoGustoEmpanadaUsuarioEliminar);
            }
            ctx.Pedido.Remove(pedido);
            ctx.SaveChanges();

            return pedido.IdPedido;
        }

        public void EnviarInvitacionesDesdeUnaListaDeUsuarios(List<Usuario> usuariosAEnviarInvitacion, Pedido pedido)
        {
            foreach (Usuario invitado in usuariosAEnviarInvitacion)
            {
                Usuario usuarioEncontrado = ctx.Usuario.SingleOrDefault(x => x.IdUsuario == invitado.IdUsuario);
                Pedido pedidoEncontrado = ctx.Pedido.SingleOrDefault(p => p.IdPedido == pedido.IdPedido);
                InvitacionPedido invitacionPedido = new InvitacionPedido
                {
                    Pedido = pedidoEncontrado,
                    Usuario = usuarioEncontrado,
                    Token = new Guid(new Md5Hash().GetMD5((usuarioEncontrado.Email + pedidoEncontrado.FechaCreacion))),
                    Completado = false,
                };
                ctx.InvitacionPedido.Add(invitacionPedido);
                ctx.SaveChanges();
                es.EnviarEmailInvitados(invitacionPedido);
            }
        }

        public int ElegirService(FormCollection form, Usuario usuarioLoguedado)
        {
            Pedido pedido = ObtenerPedidoById(int.Parse(form["idPedido"]));

            foreach (var gusto in pedido.GustoEmpanada)
            {

                if (pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(p => p.IdUsuario == usuarioLoguedado.IdUsuario && p.IdGustoEmpanada == gusto.IdGustoEmpanada).Count() > 0)
                {
                    ctx.InvitacionPedidoGustoEmpanadaUsuario.Remove(ctx.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.GustoEmpanada.IdGustoEmpanada == gusto.IdGustoEmpanada && i.IdUsuario == usuarioLoguedado.IdUsuario && i.IdPedido == pedido.IdPedido).FirstOrDefault());
                    //TODO: Encontrar la forma de que se updatee el registro
                }

                try
                {
                    var cantidadEmpanada = int.Parse(form["gustoEmpanada_" + gusto.IdGustoEmpanada]);
                    if (cantidadEmpanada != 0)
                    {

                        InvitacionPedidoGustoEmpanadaUsuario ipgeu = new InvitacionPedidoGustoEmpanadaUsuario
                        {
                            Cantidad = cantidadEmpanada,
                            GustoEmpanada = gusto,
                            IdUsuario = usuarioLoguedado.IdUsuario,
                        };


                        pedido.InvitacionPedidoGustoEmpanadaUsuario.Add(ipgeu);
                    }


                }
                catch (Exception e)
                {
                    if (pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(p => p.IdUsuario == usuarioLoguedado.IdUsuario && p.IdGustoEmpanada == gusto.IdGustoEmpanada).Count() > 0)
                    {
                        ctx.InvitacionPedidoGustoEmpanadaUsuario.Remove(ctx.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.GustoEmpanada.IdGustoEmpanada == gusto.IdGustoEmpanada && i.IdUsuario == usuarioLoguedado.IdUsuario && i.IdPedido == pedido.IdPedido).FirstOrDefault());
                    }
                    Console.WriteLine(e);

                }



            }
            var invitacion = ctx.InvitacionPedido.Where(i => i.IdPedido == pedido.IdPedido && i.IdUsuario == usuarioLoguedado.IdUsuario).FirstOrDefault();
            invitacion.Completado = true;
            ctx.SaveChanges();
            return pedido.IdPedido;
        }



        public Pedido ObtenerPedidoByToken(Guid token)
        {
            Pedido pedido = ctx.InvitacionPedido.Where(i => i.Token == token).Select(p => p.Pedido).FirstOrDefault();
            return pedido;
        }

        //TODO: Elegir service por json
        public MensajeJson ElegirServiceByJson(InvitacionGustoJson invitacionJson)
        {
            MensajeJson msjJson = new MensajeJson();
            try
            {
                Pedido pedido = ObtenerPedidoById(ctx.InvitacionPedido.Where(t => t.Token == invitacionJson.Token && t.IdUsuario == invitacionJson.IdUsuario).FirstOrDefault().IdPedido);

                foreach (var gustoSolicitados in invitacionJson.GustosEmpanadasCantidad)
                {
                    if (pedido.GustoEmpanada.Where(g => g.IdGustoEmpanada == gustoSolicitados.IdGustoEmpanada).Count() == 0)
                    {
                        msjJson.Resultado = "ERROR";
                        msjJson.Mensaje = "No se pudo efectuar la operacion porque uno o mas pedidos no estan disponibles";
                        return msjJson;
                    }

                }

                if (pedido.EstadoPedido.Nombre == "Cerrado")
                {
                    msjJson.Resultado = "ERROR";
                    msjJson.Mensaje = "No se pudo efectuar la operacion porque el pedido se ecnuentra cerrado";
                    return msjJson;
                }

                foreach (var gusto in pedido.GustoEmpanada)
                {
                    try
                    {

                        if (pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(p => p.IdUsuario == invitacionJson.IdUsuario && p.IdGustoEmpanada == gusto.IdGustoEmpanada).Count() > 0)
                        {
                            ctx.InvitacionPedidoGustoEmpanadaUsuario.Remove(ctx.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.GustoEmpanada.IdGustoEmpanada == gusto.IdGustoEmpanada && i.IdUsuario == invitacionJson.IdUsuario && i.IdPedido == pedido.IdPedido).FirstOrDefault());
                            //TODO: Encontrar la forma de que se updatee el registro
                        }

                        var cantidadEmpanada = invitacionJson.GustosEmpanadasCantidad.Where(c => c.IdGustoEmpanada == gusto.IdGustoEmpanada).FirstOrDefault().Cantidad;
                        if (cantidadEmpanada != 0)
                        {

                            InvitacionPedidoGustoEmpanadaUsuario ipgeu = new InvitacionPedidoGustoEmpanadaUsuario
                            {
                                Cantidad = cantidadEmpanada,
                                GustoEmpanada = gusto,
                                IdUsuario = invitacionJson.IdUsuario,
                            };


                            pedido.InvitacionPedidoGustoEmpanadaUsuario.Add(ipgeu);
                        }


                    }
                    catch (Exception e)
                    {
                        if (pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(p => p.IdUsuario == invitacionJson.IdUsuario && p.IdGustoEmpanada == gusto.IdGustoEmpanada).Count() > 0)
                        {
                            ctx.InvitacionPedidoGustoEmpanadaUsuario.Remove(ctx.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.GustoEmpanada.IdGustoEmpanada == gusto.IdGustoEmpanada && i.IdUsuario == invitacionJson.IdUsuario && i.IdPedido == pedido.IdPedido).FirstOrDefault());
                        }
                        msjJson.Resultado = "ERROR";
                        msjJson.Mensaje = "No se pudo efectuar la operacion porque " + e.ToString();
                        return msjJson;

                    }

                }


                var invitacion = ctx.InvitacionPedido.Where(i => i.IdPedido == pedido.IdPedido && i.IdUsuario == invitacionJson.IdUsuario).FirstOrDefault();
                invitacion.Completado = true;
                ctx.SaveChanges();
                msjJson.Resultado = "OK";
                msjJson.Mensaje = "Gustos elegidos satisfactoriamente";
                return msjJson;
            }
            catch (NullReferenceException)
            {
                msjJson.Resultado = "ERROR";
                msjJson.Mensaje = "No se pudo efectuar la operacion porque el usuario es invalido";
                return msjJson;

            }

        }
        //------------------------------Queries------------------------------

        public List<Pedido> ListarPedidosByUsuario(Usuario usuario)
        {
            var query =
                (from p in ctx.Pedido
                 join ep in ctx.EstadoPedido on p.IdEstadoPedido equals ep.IdEstadoPedido
                 join ip in ctx.InvitacionPedido on p.IdPedido equals ip.IdPedido
                 where ip.IdUsuario == usuario.IdUsuario
                 orderby p.FechaCreacion
                 select
                     p).ToList();
            return query;
        }

        public Boolean PedidoUsuarioResponsableIsTrue(int idPedido, Usuario usuario)
        {
            var query = (from p in ctx.Pedido
                         where p.IdUsuarioResponsable == usuario.IdUsuario &&
                                p.IdPedido == idPedido
                         select p).ToList();

            if (query.Count > 0)
            {
                return true;
            }
            return false;
        }

        public List<Usuario> DeterminarEnviosDeInvitacionDesdeFormCollection(FormCollection form)
        {
            int IdEnviarInvitacion = ObtenerEnviarInvitacion(form);

            List<Usuario> usuariosAInvitar = new List<Usuario>();

            switch (IdEnviarInvitacion)
            {
                case 1:
                    //A nadie
                    break;
                case 2:
                    //Re enviar a todos
                    usuariosAInvitar = ObtenerTodosLosUsuariosInvitados(form);
                    break;
                case 3:
                    //Enviar solo a los nuevos
                    usuariosAInvitar = ObtenerLosUsuariosQueAntesNoEstabanInvitados(form);
                    break;
                case 4:
                    //Re enviar a los que no eligieron gustos
                    usuariosAInvitar = ObtenerLosUsuariosInvitadosQueNoEligieronGustos(form);
                    break;
            }

            return usuariosAInvitar;
        }

        public List<Usuario> ObtenerTodosLosUsuariosInvitadosDesdeUnPedido(Pedido pedido)
        {
            var query =
                (from ip in ctx.InvitacionPedido
                 join p in ctx.Pedido on ip.IdPedido equals p.IdPedido
                 join u in ctx.Usuario on ip.IdUsuario equals u.IdUsuario
                 where ip.IdPedido == pedido.IdPedido
                 select
                    u).Distinct().ToList();
            return query;
        }

        public List<Usuario> QuitarUserActivoDeUnaListaDeUsuarios(List<Usuario> usuarios, Usuario usuarioLogueado)
        {
            List<Usuario> listaFiltrada = new List<Usuario>();

            foreach (Usuario usuario in usuarios)
            {
                if (usuario.IdUsuario == usuarioLogueado.IdUsuario)
                {
                    
                }
                else
                {
                    listaFiltrada.Add(usuario);
                }
            }

            return listaFiltrada;
        }

        internal dynamic ObtenerTodosLosUsuariosInvitadosDesdeUnPedidoSinUserResponsable(Pedido pedidoAEditar, Usuario usuarioLoguedado)
        {
            var query =
                (from ip in ctx.InvitacionPedido
                 join p in ctx.Pedido on ip.IdPedido equals p.IdPedido
                 join u in ctx.Usuario on ip.IdUsuario equals u.IdUsuario
                 where ip.IdPedido == p.IdPedido 
                 where u.IdUsuario != usuarioLoguedado.IdUsuario
                 select
                    u).Distinct().ToList();
            return query;
        }

        public List<Usuario> ObtenerTodosLosUsuariosInvitados(FormCollection form)
        {
            List<Usuario> listaUsuario = new List<Usuario>();
            string[] usuariosInvitados = form.GetValues("invitados");
            foreach (var usuario in usuariosInvitados)
            {
                Usuario usuarioEncontrado = ctx.Usuario.Find(int.Parse(usuario));
                listaUsuario.Add(usuarioEncontrado);
            }

            return listaUsuario;
        }

        public List<Usuario> ObtenerLosUsuariosQueAntesNoEstabanInvitados(FormCollection form)
        {
            List<Usuario> usuariosAInvitar = new List<Usuario>();

            List<Usuario> usuariosListados = new List<Usuario>();

            Pedido pedidoAEditar = ObtenerPedidoPorId(int.Parse(form["IdPedido"]));

            int[] invitados = Array.ConvertAll(form.GetValues("invitados"), int.Parse);

            foreach (var usuario in invitados)
            {
                Usuario usuarioBuscado = BuscarUsuarioById(usuario);
                usuariosListados.Add(usuarioBuscado);
            }

            foreach (Usuario usuario in usuariosListados)
            {
                foreach (InvitacionPedido invitacionPedido in pedidoAEditar.InvitacionPedido)
                {
                    if (usuario.InvitacionPedido.Contains(invitacionPedido))
                    {
                        break;
                    }
                    else
                    {
                        usuariosAInvitar.Add(usuario);
                        break;
                    }
                }
            }

            return usuariosAInvitar;
        }

        private List<Usuario> ObtenerLosUsuariosInvitadosQueNoEligieronGustos(FormCollection form)
        {
            List<Usuario> usuariosAInvitar = new List<Usuario>();

            List<Usuario> usuariosListados = new List<Usuario>();

            Pedido pedidoAEditar = ObtenerPedidoPorId(int.Parse(form["IdPedido"]));

            int[] invitados = Array.ConvertAll(form.GetValues("invitados"), int.Parse);

            foreach (var usuario in invitados)
            {
                Usuario usuarioBuscado = BuscarUsuarioById(usuario);
                usuariosListados.Add(usuarioBuscado);
            }

            foreach (Usuario usuario in usuariosListados)
            {
                if (usuario.InvitacionPedido.Where(u => u.IdPedido == pedidoAEditar.IdPedido).Select(i => i.Completado).FirstOrDefault() == true)
                {

                }
                else
                {
                    usuariosAInvitar.Add(usuario);
                }
            }

            return usuariosAInvitar;
        }

        public Pedido ObtenerPedidoDesdeFormCollection(FormCollection form)
        {
            Pedido pedidoEditado = new Pedido
            {
                IdPedido = int.Parse(form["IdPedido"]),
                NombreNegocio = form["NombreNegocio"],
                Descripcion = form["Descripcion"],
                PrecioUnidad = int.Parse(form["PrecioUnidad"]),
                PrecioDocena = int.Parse(form["PrecioDocena"]),
                FechaCreacion = DateTime.Now,
            };
            return pedidoEditado;
        }

        public void ActualizarValoresDeUnPedidoDesdeFormCollection(FormCollection form)
        {
            Pedido pedidoEditado = ObtenerPedidoDesdeFormCollection(form);

            Pedido pedidoAEditar = ObtenerPedidoPorId(int.Parse(form["idPedido"]));

            int[] gustosDisponibles = Array.ConvertAll(form.GetValues("gustosDisponibles"), int.Parse);
            int[] invitados = Array.ConvertAll(form.GetValues("invitados"), int.Parse);


            // Tratamiento Gustos Empanada
            List<GustoEmpanada> gustosEmpanada = new List<GustoEmpanada>();

            foreach (int gusto in gustosDisponibles)
            {
                GustoEmpanada gustoEmp = BuscarGustoPorId(gusto);
                gustosEmpanada.Add(gustoEmp);
            }

            foreach (GustoEmpanada gusto in gustosEmpanada)
            {
                if (pedidoAEditar.GustoEmpanada.Contains(gusto))
                {
                }
                else
                {
                    GustoEmpanada gustoEncontrado = ctx.GustoEmpanada.SingleOrDefault(x => x.IdGustoEmpanada == gusto.IdGustoEmpanada);
                    pedidoAEditar.GustoEmpanada.Add(gustoEncontrado);
                }
            }

            // Tratamiento Invitados
            List<Usuario> invitadosLista = new List<Usuario>();

            foreach (int usuarios in invitados)
            {
                Usuario user = BuscarUsuarioById(usuarios);
                invitadosLista.Add(user);
            }

            List<InvitacionPedido> invitaciones = new List<InvitacionPedido>();

            foreach (var invitacion in pedidoAEditar.InvitacionPedido)
            {
                invitaciones.Add(invitacion);
            }

            foreach (Usuario usuario in invitadosLista)
            {
                foreach (InvitacionPedido invitacionPedido in invitaciones)
                {
                    if (usuario.IdUsuario == invitacionPedido.IdUsuario)
                    {
                        break;
                    }
                    else
                    {
                        Usuario invitadoEncontrado = ctx.Usuario.SingleOrDefault(x => x.IdUsuario == usuario.IdUsuario);

                        InvitacionPedido invitacionPedidoNueva = new InvitacionPedido
                        {
                            Pedido = pedidoAEditar,
                            Usuario = invitadoEncontrado,
                            Token = new Guid(new Md5Hash().GetMD5((invitadoEncontrado.Email + pedidoAEditar.FechaCreacion))),
                            Completado = false,
                        };
                        ctx.InvitacionPedido.Add(invitacionPedidoNueva);

                        break;
                    }
                }
            }
            //pedidoAEditar.GustoEmpanada = pedidoEditado.GustoEmpanada;
            //pedidoAEditar.InvitacionPedido = pedidoEditado.InvitacionPedido;
            //pedidoAEditar.InvitacionPedidoGustoEmpanadaUsuario = pedidoEditado.InvitacionPedidoGustoEmpanadaUsuario;
            pedidoAEditar.NombreNegocio = pedidoEditado.NombreNegocio;
            pedidoAEditar.Descripcion = pedidoEditado.Descripcion;
            pedidoAEditar.PrecioDocena = pedidoEditado.PrecioDocena;
            pedidoAEditar.PrecioUnidad = pedidoEditado.PrecioUnidad;
            ctx.SaveChanges();
        }

        private GustoEmpanada BuscarGustoPorId(int idGusto)
        {
            GustoEmpanada gustoEmpanada = ctx.GustoEmpanada.Find(idGusto);
            return gustoEmpanada;
        }

        public int ObtenerEnviarInvitacion(FormCollection form)
        {
            int envioInvitacion = int.Parse(form["enviarInvitacion"]);

            return envioInvitacion;
        }

        public Boolean InvitacionPedidoUsuarioIsTrue(int idPedido, Usuario usuario)
        {
            var query = (from ip in ctx.InvitacionPedido
                         where ip.IdUsuario == usuario.IdUsuario && ip.IdPedido == idPedido
                         select ip).ToList();

            if (query.Count > 0)
            {
                return true;
            }
            return false;
        }

        public List<GustoEmpanada> ObtenerGustoDeEmpanadasList()
        {
            var query = (from ge in ctx.GustoEmpanada
                         select ge).ToList();

            return query;
        }

        public List<Usuario> ObtenerUsuarioList(Usuario usuarioLogueado)
        {
            var query = (from u in ctx.Usuario
                         where u.IdUsuario != usuarioLogueado.IdUsuario
                         select u).ToList();

            return query;
        }

        public Pedido ObtenerPedidoById(int id)
        {
            Pedido pedido = ctx.Pedido.Find(id);
            return pedido;
        }

        public GustoEmpanada ObtenerGustoEmpanadaById(int id)
        {
            GustoEmpanada gusto = ctx.GustoEmpanada.Find(id);
            return gusto;
        }

        public List<InvitacionPedidoGustoEmpanadaUsuario> ObtenerIPGEUByIdPedido(int id, Usuario usuarioLogueado)
        {
            var query = (from i in ctx.InvitacionPedidoGustoEmpanadaUsuario
                         where i.IdPedido == id && i.IdUsuario == usuarioLogueado.IdUsuario
                         select i).ToList();
            return query;
        }

        public Pedido ObtenerPedidoPorId(int id)
        {
            var query = (from p in ctx.Pedido
                         where p.IdPedido == id
                         select p).FirstOrDefault();
            return query;
        }

        public void FinalizarPedidoPorId(int id)
        {
            Pedido pedidoAFinalizar = ObtenerPedidoById(id);
            AvisarUsuarioResponsable(pedidoAFinalizar);
            pedidoAFinalizar.EstadoPedido = ctx.EstadoPedido.Where(e => e.IdEstadoPedido == 2).FirstOrDefault();

            ctx.SaveChanges();
        }

        public void AvisarUsuarioResponsable(Pedido pedidoAFinalizar)
        {
            InfoEmailResponsable infoResponsable = CalcularPedidoResponsable(pedidoAFinalizar);
            es.EnviarEmailResponsable(pedidoAFinalizar, infoResponsable);
            es.EnviarEmailInvitados(pedidoAFinalizar, infoResponsable);
        }

        public InfoEmailResponsable CalcularPedidoResponsable(Pedido pedido)
        {
            var precioUnidad = pedido.PrecioUnidad;
            var precioDocena = pedido.PrecioDocena;
            var cantEmpanadas = pedido.InvitacionPedidoGustoEmpanadaUsuario.Select(c => c.Cantidad).Sum();

            int cantDocenas = cantEmpanadas / 12;
            int cantEmpanadasSingulares = cantEmpanadas - (cantDocenas * 12);

            float precioTotalEmpanadasSingulares = cantEmpanadasSingulares * precioUnidad;
            float precioTotalDocena = cantDocenas * precioDocena;

            float precioTotal = precioTotalEmpanadasSingulares + precioTotalDocena;
            float precioPorEmpanada = precioTotal / cantEmpanadas;

            List<InfoInvitadoEmail> listaInvitados = new List<InfoInvitadoEmail>();
            List<InfoGustosEmail> listaGustos = new List<InfoGustosEmail>();

            foreach (var invitacion in pedido.InvitacionPedido)
            {
                int cantidadEmpanadaPerCapita = invitacion.Pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.IdUsuario == invitacion.IdUsuario).Select(i => i.Cantidad).Sum();
                float precioPerCapita = precioPorEmpanada * cantidadEmpanadaPerCapita;

                List<InfoGustosEmail> listaEmpanadas = new List<InfoGustosEmail>();

                foreach (var gusto in pedido.GustoEmpanada)
                {
                    var cantidadEmpanadaPorGusto = pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.GustoEmpanada.IdGustoEmpanada == gusto.IdGustoEmpanada && i.IdUsuario == invitacion.IdUsuario).Select(i => i.Cantidad).FirstOrDefault();
                    InfoGustosEmail empanada = new InfoGustosEmail
                    {
                        Gusto = gusto.Nombre,
                        Cantidad = cantidadEmpanadaPorGusto
                    };
                    listaEmpanadas.Add(empanada);
                }

                InfoInvitadoEmail infoEmailResponsable = new InfoInvitadoEmail
                {
                    Email = invitacion.Usuario.Email,
                    Precio = precioPerCapita,
                    CantidadTotal = cantidadEmpanadaPerCapita,
                    Empanadas = listaEmpanadas

                };


                listaInvitados.Add(infoEmailResponsable);
            }

            foreach (var gusto in pedido.GustoEmpanada)
            {
                var cantidadEmpanadaPorGusto = pedido.InvitacionPedidoGustoEmpanadaUsuario.Where(i => i.GustoEmpanada.IdGustoEmpanada == gusto.IdGustoEmpanada).Select(i => i.Cantidad).Sum();
                InfoGustosEmail infoGustosResponsable = new InfoGustosEmail
                {
                    Gusto = gusto.Nombre,
                    Cantidad = cantidadEmpanadaPorGusto
                };
                listaGustos.Add(infoGustosResponsable);
            }

            InfoEmailResponsable infoResponsable = new InfoEmailResponsable
            {
                PrecioTotal = precioTotal,
                Invitados = listaInvitados,
                Gustos = listaGustos
            };
            return infoResponsable;
        }
    }
}