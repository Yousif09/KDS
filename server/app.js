const express = require("express");
const app = express();

app.get("/data", (req, res) => {
  res.json({
    name: "Kitchen Display",
    description: "For Taj Ali",
  });
});

app.listen('https://yousif09.github.io/KDS/', () => {
  console.log("server running on https://yousif09.github.io/KDS/");
});
