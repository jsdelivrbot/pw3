﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using TrabajoPracticoPw3.Models;
using Newtonsoft.Json;
using System.Web.Http.Cors;
using TrabajoPracticoPw3.Api.Model;
using TrabajoPracticoPw3.Services;

namespace TrabajoPracticoPw3.Api
{
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    public class PedidoController : ApiController
    {
        //public class PedidoJson {

        //    public int IdPedido { get; set; }
        //    public string Descripcion { get; set; }
        //}

        TPEntities ctx = new TPEntities();
        PedidoService ps = new PedidoService();

        //GET api/values
        //public string Get()
        //{
        //    foreach (var pedido in ctx.Pedido.ToList())
        //    {
        //        var pJson = new PedidoJson();

        //    }


        //    var pedidoJson = new
        //    {

        //        IdPedido = 1,
        //        Descripcion = "pedido ssssss",
        //    };

        //    string json = JsonConvert.SerializeObject(listaPedidos);
        //    return json;

        //}

        //[Route("api/Pedido/ConfirmarGustos")]
        [ActionName("ConfirmarGustos")]
        [HttpPost]
        public string ConfirmarGustos([FromBody] InvitacionGustoJson model)
        {
            MensajeJson mensajeJson = ps.ElegirServiceByJson(model);
            return JsonConvert.SerializeObject(mensajeJson);
        }

        // GET api/values/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        //public void Post([FromBody]string value)
        //{
        //}

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {

        }
    }
}
