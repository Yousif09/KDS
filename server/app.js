const express = require("express"); 
const app = express(); 
const cors = require('cors'); 


app.use(cors()); 



app.get("/", (req, res) => {

	res.json({name: "Taj Ali KDS"});

});

app.listen('https://yousif09.github.io/KDS/', () => console.log("server running")); 
